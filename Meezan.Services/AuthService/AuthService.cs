using Common.Dto;
using Common.Enums;
using Common.PasswordHash;
using Common.Validator;
using MapsterMapper;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Auth;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Auth;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using Meezan.Services.Setting;
using System.Security.Cryptography;
using System.Text;

namespace Meezan.Services.AuthService
{
    public class AuthService : BaseService, IAuthService
    {
        private IPasswordHash PasswordHash { get; }
        private MailSettings MailSetting { get; }
        private IJwtTokenGenerator JwtTokenGenerator { get; }
        private IValidatorHelper ValidatorHelper { get; }
        private JwtSettings JwtSettings { get; }
        private RefreshTokenSettings RefreshTokenSettings { get; }

        public AuthService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localization,
            IPasswordHash passwordHash,
            MailSettings mailSettings,
            IJwtTokenGenerator jwtTokenGenerator,
            IValidatorHelper validatorHelper,
            JwtSettings jwtSettings,
            RefreshTokenSettings refreshTokenSettings) : base(unitOfWork, mapper, localization)
        {
            PasswordHash = passwordHash;
            MailSetting = mailSettings;
            JwtTokenGenerator = jwtTokenGenerator;
            ValidatorHelper = validatorHelper;
            JwtSettings = jwtSettings;
            RefreshTokenSettings = refreshTokenSettings;
        }

        #region Public Methods

        public async Task<ResponseDto<EmptyResponseDto>> Register(RegisterDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            ValidateRegistration(request);

            // Pre-check for fast UX — unique DB constraints are the authoritative guard
            if (await UnitOfWork.UserRepository.AnyAsync(u => u.UserName == request.UserName || u.Email == request.Email))
                throw new InvalidRequestException(Localization.UserNameOrEmailAlreadyExists);

            Mail mail = CreateWelcomeMail(request.FirstName, request.Email);
            UnitOfWork.UserRepository.Create(CreateUser(request));
            UnitOfWork.MailRepository.Create(mail);
            UnitOfWork.OutboxRepository.Create(new OutboxMessage { Mail = mail });

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.RegistrationSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<LoginResponseDto>> Login(LoginDto request, string? ipAddress, string? deviceId, string? deviceName, string? platform, CancellationToken cancellationToken = default)
        {
            ResponseDto<LoginResponseDto> response = new ResponseDto<LoginResponseDto>().GetErrorResponse();

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidRequestException(Localization.InvalidRequest);

            User user = await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user is null || !PasswordHash.ValidatePassword(request.Password, user.Password))
                throw new InvalidRequestException(Localization.InvalidCredentials);

            List<RefreshToken> activeDevices = await UnitOfWork.RefreshTokenRepository.GetActiveByUserIdAsync(user.Id);
            if (RefreshTokenSettings.MaxActiveDevices > 0 && activeDevices.Count >= RefreshTokenSettings.MaxActiveDevices)
            {
                if (string.Equals(RefreshTokenSettings.MaxActiveDevicesStrategy, "RejectNewLogin", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidRequestException(Localization.MaxActiveDevicesReached);

                RefreshToken oldest = activeDevices.OrderBy(t => t.CreationTime).First();
                oldest.RevokedAt = DateTime.UtcNow;
                oldest.RevokedReason = RefreshTokenRevocationReason.DeviceLimitReached;
                UnitOfWork.RefreshTokenRepository.Update(oldest);
            }

            (string rawRefreshToken, RefreshToken refreshTokenEntity) = CreateRefreshToken(
                user.Id, Guid.NewGuid(), DateTime.UtcNow, ipAddress, deviceId, deviceName, platform);
            UnitOfWork.RefreshTokenRepository.Create(refreshTokenEntity);

            LoginResponseDto loginResponse = new()
            {
                Token = JwtTokenGenerator.GenerateToken(user),
                ExpiresAt = DateTime.UtcNow.AddMinutes(JwtSettings.ExpiryMinutes),
                RefreshToken = rawRefreshToken
            };

            await UnitOfWork.CommitAsync(cancellationToken);

            return response.GetSuccessResponse(loginResponse);
        }

        public async Task<ResponseDto<LoginResponseDto>> RefreshToken(string? presentedToken, string? ipAddress, string? deviceId, string? deviceName, string? platform, CancellationToken cancellationToken = default)
        {
            ResponseDto<LoginResponseDto> response = new ResponseDto<LoginResponseDto>().GetErrorResponse();

            if (string.IsNullOrWhiteSpace(presentedToken))
                throw new InvalidRequestException(Localization.InvalidRequest);

            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(presentedToken)));
            RefreshToken existing = await UnitOfWork.RefreshTokenRepository.GetByTokenHashAsync(tokenHash);

            if (existing is null)
                throw new InvalidRequestException(Localization.InvalidRefreshToken);

            // Reuse detection: an already-revoked token being presented again = theft.
            // Kill only this token's rotation chain (FamilyId), not the user's other devices.
            if (existing.RevokedAt is not null)
            {
                List<RefreshToken> family = await UnitOfWork.RefreshTokenRepository.GetActiveByFamilyIdAsync(existing.FamilyId);
                await UnitOfWork.ExecuteInTransactionAsync(_ =>
                {
                    family.ForEach(t =>
                    {
                        t.RevokedAt = DateTime.UtcNow;
                        t.RevokedReason = RefreshTokenRevocationReason.ReuseDetected;
                        UnitOfWork.RefreshTokenRepository.Update(t);
                    });
                    return Task.CompletedTask;
                }, cancellationToken);

                throw new InvalidRequestException(Localization.InvalidRefreshToken);
            }

            if (existing.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidRequestException(Localization.InvalidRefreshToken);

            if (DateTime.UtcNow - existing.FamilyCreatedAt > TimeSpan.FromDays(RefreshTokenSettings.AbsoluteSessionLifetimeDays))
            {
                List<RefreshToken> family = await UnitOfWork.RefreshTokenRepository.GetActiveByFamilyIdAsync(existing.FamilyId);
                family.ForEach(t =>
                {
                    t.RevokedAt = DateTime.UtcNow;
                    t.RevokedReason = RefreshTokenRevocationReason.AbsoluteLifetimeExceeded;
                    UnitOfWork.RefreshTokenRepository.Update(t);
                });
                await UnitOfWork.CommitAsync(cancellationToken);

                throw new InvalidRequestException(Localization.SessionExpired);
            }

            User user = await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.Id == existing.UserId);
            if (user is null)
                throw new ObjectNotFoundException("User not found");

            (string rawRefreshToken, RefreshToken newEntity) = CreateRefreshToken(
                user.Id, existing.FamilyId, existing.FamilyCreatedAt, ipAddress,
                deviceId ?? existing.DeviceId, deviceName ?? existing.DeviceName, platform ?? existing.Platform);

            try
            {
                // Rotation must be atomic: revoke-old + create-new either both land or neither does.
                await UnitOfWork.ExecuteInTransactionAsync(_ =>
                {
                    existing.RevokedAt = DateTime.UtcNow;
                    existing.RevokedReason = RefreshTokenRevocationReason.RefreshRotation;
                    existing.LastUsedAt = DateTime.UtcNow;
                    existing.LastUsedIp = ipAddress;
                    UnitOfWork.RefreshTokenRepository.Update(existing);
                    UnitOfWork.RefreshTokenRepository.Create(newEntity);
                    return Task.CompletedTask;
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Lost the race against a concurrent refresh call using the same token.
                throw new InvalidRequestException(Localization.RefreshTokenConflict);
            }

            LoginResponseDto loginResponse = new()
            {
                Token = JwtTokenGenerator.GenerateToken(user),
                ExpiresAt = DateTime.UtcNow.AddMinutes(JwtSettings.ExpiryMinutes),
                RefreshToken = rawRefreshToken
            };

            return response.GetSuccessResponse(loginResponse);
        }

        public async Task<ResponseDto<EmptyResponseDto>> Logout(string? presentedToken, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (string.IsNullOrWhiteSpace(presentedToken))
                throw new InvalidRequestException(Localization.InvalidRequest);

            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(presentedToken)));
            RefreshToken existing = await UnitOfWork.RefreshTokenRepository.GetByTokenHashAsync(tokenHash);

            // Silent success either way — same non-enumeration philosophy as ForgotPassword.
            if (existing is null || existing.RevokedAt is not null)
                return response.GetSuccessResponse(Localization.LogoutSuccess);

            existing.RevokedAt = DateTime.UtcNow;
            existing.RevokedReason = RefreshTokenRevocationReason.Logout;
            UnitOfWork.RefreshTokenRepository.Update(existing);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.LogoutSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> LogoutAllDevices(string userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (!int.TryParse(userId, out int id))
                throw new InvalidRequestException(Localization.InvalidRequest);

            List<RefreshToken> activeTokens = await UnitOfWork.RefreshTokenRepository.GetActiveByUserIdAsync(id);
            if (activeTokens.Count == 0)
                return response.GetSuccessResponse(Localization.LogoutAllSuccess);

            activeTokens.ForEach(t =>
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedReason = RefreshTokenRevocationReason.LogoutAll;
                UnitOfWork.RefreshTokenRepository.Update(t);
            });

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.LogoutAllSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<List<SessionDto>>> GetActiveSessions(string userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<SessionDto>> response = new ResponseDto<List<SessionDto>>().GetErrorResponse();

            if (!int.TryParse(userId, out int id))
                throw new InvalidRequestException(Localization.InvalidRequest);

            List<RefreshToken> activeTokens = await UnitOfWork.RefreshTokenRepository.GetActiveByUserIdAsync(id);

            List<SessionDto> sessions = activeTokens.Select(t => new SessionDto
            {
                Id = t.Id,
                DeviceId = t.DeviceId,
                DeviceName = t.DeviceName,
                Platform = t.Platform,
                CreatedAt = t.CreationTime,
                LastUsedAt = t.LastUsedAt,
                LastUsedIp = t.LastUsedIp
            }).ToList();

            return response.GetSuccessResponse(sessions);
        }

        public async Task<ResponseDto<EmptyResponseDto>> RevokeSession(string userId, int refreshTokenId, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (!int.TryParse(userId, out int id))
                throw new InvalidRequestException(Localization.InvalidRequest);

            RefreshToken token = await UnitOfWork.RefreshTokenRepository.FirstOrDefaultAsync(t => t.Id == refreshTokenId && t.UserId == id);
            if (token is null)
                throw new ObjectNotFoundException("Session not found");

            // Idempotent — revoking an already-revoked session is a no-op success.
            if (token.RevokedAt is not null)
                return response.GetSuccessResponse();

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = RefreshTokenRevocationReason.AdminRevoked;
            UnitOfWork.RefreshTokenRepository.Update(token);

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse()
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> ChangePassword(string userId, ChangePasswordDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (!int.TryParse(userId, out int id))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (request.NewPassword != request.ConfirmNewPassword)
                throw new InvalidRequestException(Localization.PasswordMismatch);

            (bool isValid, string errorMessage) = ValidatorHelper.ValidatePasswordPolicy(request.NewPassword);
            if (!isValid)
                throw new InvalidRequestException(errorMessage);

            User user = await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.Id == id);
            if (user is null)
                throw new ObjectNotFoundException("User not found");

            if (!PasswordHash.ValidatePassword(request.CurrentPassword, user.Password))
                throw new InvalidRequestException(Localization.InvalidCurrentPassword);

            user.Password = PasswordHash.CreateHash(request.NewPassword);
            UnitOfWork.UserRepository.Update(user);

            // A changed password should force re-authentication on every device.
            List<RefreshToken> activeRefreshTokens = await UnitOfWork.RefreshTokenRepository.GetActiveByUserIdAsync(id);
            activeRefreshTokens.ForEach(t =>
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedReason = RefreshTokenRevocationReason.PasswordChanged;
                UnitOfWork.RefreshTokenRepository.Update(t);
            });

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse()//ToDo: add success msg
                : response.GetErrorResponse(Localization.GeneralError);
        }

        public async Task<ResponseDto<EmptyResponseDto>> ForgotPassword(ForgotPasswordDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            User user = await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.Email == request.Email);

            // Silent success — prevents user enumeration
            if (user is null)
                return response.GetSuccessResponse(Localization.PasswordResetSent);

            // Revoke all active tokens before issuing a new one
            List<PasswordResetToken> activeTokens =
                await UnitOfWork.PasswordResetTokenRepository.GetActiveByUserIdAsync(user.Id);
            activeTokens.ForEach(t => { t.IsUsed = true; UnitOfWork.PasswordResetTokenRepository.Update(t); });

            string rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            UnitOfWork.PasswordResetTokenRepository.Create(new PasswordResetToken
            {
                TokenHash = tokenHash,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(JwtSettings.ResetTokenExpiryMinutes),
                IsUsed = false
            });

            string resetLink = $"{JwtSettings.FrontEndBaseUrl}/reset-password?token={rawToken}";
            Mail mail = new()
            {
                Subject = "Password Reset Request",
                To = user.Email,
                Body = $"Click the following link to reset your password: {resetLink}",
                MailStatusId = (int)MailStatusEnum.Draft,
                MailTypeId = (int)MailTypeEnum.ForgetPassword,
            };
            UnitOfWork.MailRepository.Create(mail);
            UnitOfWork.OutboxRepository.Create(new OutboxMessage { Mail = mail });

            await UnitOfWork.CommitAsync(cancellationToken);

            return response.GetSuccessResponse(Localization.PasswordResetSent);
        }

        public async Task<ResponseDto<EmptyResponseDto>> ResetPassword(ResetPasswordDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (string.IsNullOrWhiteSpace(request.Token))
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (request.NewPassword != request.ConfirmNewPassword)
                throw new InvalidRequestException(Localization.PasswordMismatch);

            (bool isValid, string errorMessage) = ValidatorHelper.ValidatePasswordPolicy(request.NewPassword);
            if (!isValid)
                throw new InvalidRequestException(errorMessage);

            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
            PasswordResetToken resetToken =
                await UnitOfWork.PasswordResetTokenRepository.GetByTokenHashAsync(tokenHash);
            if (resetToken is null)
                throw new InvalidRequestException(Localization.InvalidResetToken);

            User user = await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.Id == resetToken.UserId);
            if (user is null)
                throw new ObjectNotFoundException("User not found");

            user.Password = PasswordHash.CreateHash(request.NewPassword);
            UnitOfWork.UserRepository.Update(user);

            resetToken.IsUsed = true;
            UnitOfWork.PasswordResetTokenRepository.Update(resetToken);

            List<PasswordResetToken> remainingTokens =
                await UnitOfWork.PasswordResetTokenRepository.GetActiveByUserIdAsync(resetToken.UserId);
            remainingTokens.ForEach(t => { t.IsUsed = true; UnitOfWork.PasswordResetTokenRepository.Update(t); });

            // A reset password should force re-authentication on every device.
            List<RefreshToken> activeRefreshTokens =
                await UnitOfWork.RefreshTokenRepository.GetActiveByUserIdAsync(resetToken.UserId);
            activeRefreshTokens.ForEach(t =>
            {
                t.RevokedAt = DateTime.UtcNow;
                t.RevokedReason = RefreshTokenRevocationReason.PasswordReset;
                UnitOfWork.RefreshTokenRepository.Update(t);
            });

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse(Localization.PasswordResetSuccess)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        #endregion

        #region Private Methods

        private void ValidateRegistration(RegisterDto request)
        {
            ValidateRegisterRequest(request);

            if (request.Password != request.ConfirmPassword)
                throw new InvalidRequestException(Localization.PasswordMismatch);

            (bool isValid, string errorMessage) = ValidatorHelper.ValidatePasswordPolicy(request.Password);
            if (!isValid)
                throw new InvalidRequestException(errorMessage);
        }

        private User CreateUser(RegisterDto request) => new()
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.UserName,
            Password = PasswordHash.CreateHash(request.Password)
        };

        private Mail CreateWelcomeMail(string firstName, string email) => new()
        {
            Subject = MailSetting.Subject,
            To = email,
            Body = string.Format(MailSetting.Body, firstName, email),
            MailStatusId = (int)MailStatusEnum.Draft,
            MailTypeId = (int)MailTypeEnum.WelcomeMail,
        };

        private void ValidateRegisterRequest(RegisterDto request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
                throw new NameRequiredException("First name is required");
            if (string.IsNullOrWhiteSpace(request.LastName))
                throw new NameRequiredException("Last name is required");
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new InvalidRequestException("Email is required");
            if (string.IsNullOrWhiteSpace(request.UserName))
                throw new NameRequiredException("Username is required");
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new NameRequiredException("Password is required");
            if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
                throw new NameRequiredException("Confirm password is required");
        }

        private (string rawToken, RefreshToken entity) CreateRefreshToken(
            int userId, Guid familyId, DateTime familyCreatedAt, string? ipAddress, string? deviceId, string? deviceName, string? platform)
        {
            string rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            RefreshToken entity = new()
            {
                TokenHash = tokenHash,
                UserId = userId,
                FamilyId = familyId,
                FamilyCreatedAt = familyCreatedAt,
                DeviceId = deviceId,
                DeviceName = deviceName,
                Platform = platform,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenSettings.RefreshTokenExpiryDays),
                CreatedByIp = ipAddress,
                LastUsedIp = ipAddress
            };

            return (rawToken, entity);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
            ex.InnerException?.Message.Contains("UNIQUE KEY constraint") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true;

        private string ResolveUniqueConstraintError(DbUpdateException ex)
        {
            string msg = ex.InnerException?.Message ?? string.Empty;
            if (msg.Contains("IX_User_Email"))
                return Localization.EmailAlreadyExists;
            if (msg.Contains("IX_User_UserName"))
                return Localization.UserNameAlreadyExists;
            return Localization.UserNameOrEmailAlreadyExists;
        }

        #endregion
    }
}
