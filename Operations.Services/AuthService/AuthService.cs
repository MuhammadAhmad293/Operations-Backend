using Common.Dto;
using Common.Enums;
using Common.Notification.Mail;
using Common.PasswordHash;
using Common.Validator;
using Microsoft.EntityFrameworkCore;
using Operations.DataModel.Entities;
using Operations.Dto.DTOs.Auth;
using Operations.IRepositories.UnitOfWork;
using Operations.IServices.IService;
using Operations.Services.Auth;
using Operations.Services.Base;
using Operations.Services.CustomExceptions;
using Operations.Services.Localization;
using Operations.Services.Setting;
using MapsterMapper;
using System.Security.Cryptography;
using System.Text;

namespace Operations.Services.AuthService
{
    public class AuthService : BaseService, IAuthService
    {
        private IPasswordHash PasswordHash { get; }
        private IMailSender MailSender { get; }
        private MailSettings MailSetting { get; }
        private IJwtTokenGenerator JwtTokenGenerator { get; }
        private IValidatorHelper ValidatorHelper { get; }
        private JwtSettings JwtSettings { get; }

        public AuthService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localization,
            IPasswordHash passwordHash,
            IMailSender mailSender,
            MailSettings mailSettings,
            IJwtTokenGenerator jwtTokenGenerator,
            IValidatorHelper validatorHelper,
            JwtSettings jwtSettings) : base(unitOfWork, mapper, localization)
        {
            PasswordHash = passwordHash;
            MailSender = mailSender;
            MailSetting = mailSettings;
            JwtTokenGenerator = jwtTokenGenerator;
            ValidatorHelper = validatorHelper;
            JwtSettings = jwtSettings;
        }

        #region Public Methods

        public async Task<ResponseDto<EmptyResponseDto>> Register(RegisterDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            ValidateRegistration(request);

            // Pre-check for fast UX — unique DB constraints are the authoritative guard
            if (await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.UserName == request.UserName || u.Email == request.Email) is not null)
                throw new InvalidRequestException(Localization.UserNameOrEmailAlreadyExists);

            Mail mail = CreateWelcomeMail(request.FirstName, request.Email);
            UnitOfWork.UserRepository.Create(CreateUser(request));
            UnitOfWork.MailRepository.Create(mail);

            try
            {
                await UnitOfWork.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                throw new InvalidRequestException(ResolveUniqueConstraintError(ex));
            }

            (MailDto mailDto, MailSettingDto mailSettingDto) = PrepareMailDtos(mail);
            await MailSender.SendMail(mailDto, mailSettingDto, cancellationToken);

            return response.GetSuccessResponse(Localization.RegistrationSuccess);
        }

        public async Task<ResponseDto<LoginResponseDto>> Login(LoginDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<LoginResponseDto> response = new ResponseDto<LoginResponseDto>().GetErrorResponse();

            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidRequestException(Localization.InvalidRequest);

            User user = await UnitOfWork.UserRepository.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user is null || !PasswordHash.ValidatePassword(request.Password, user.Password))
                throw new InvalidRequestException(Localization.InvalidCredentials);

            LoginResponseDto loginResponse = new()
            {
                Token = JwtTokenGenerator.GenerateToken(user),
                ExpiresAt = DateTime.UtcNow.AddMinutes(JwtSettings.ExpiryMinutes)
            };

            return response.GetSuccessResponse(loginResponse);
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

            return await UnitOfWork.CommitAsync(cancellationToken) > default(int)
                ? response.GetSuccessResponse()
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

            string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            UnitOfWork.PasswordResetTokenRepository.Create(new PasswordResetToken
            {
                TokenHash = tokenHash,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(JwtSettings.ResetTokenExpiryMinutes),
                IsUsed = false
            });

            string resetLink = $"{JwtSettings.FrontEndBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
            Mail mail = new()
            {
                Subject = "Password Reset Request",
                To = user.Email,
                Body = $"Click the following link to reset your password: {resetLink}",
                MailStatusId = (int)MailStatusEnum.New,
                MailTypeId = (int)MailTypeEnum.ForgetPassword,
            };
            UnitOfWork.MailRepository.Create(mail);

            await UnitOfWork.CommitAsync(cancellationToken);

            (MailDto mailDto, MailSettingDto mailSettingDto) = PrepareMailDtos(mail);
            await MailSender.SendMail(mailDto, mailSettingDto, cancellationToken);

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
            MailStatusId = (int)MailStatusEnum.New,
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

        private (MailDto, MailSettingDto) PrepareMailDtos(Mail mail)
        {
            MailDto mailDto = new()
            {
                Id = mail.MailId,
                MailTo = new List<string> { mail.To },
                Subject = mail.Subject,
                Body = mail.Body,
                IsBodyHtml = false,
            };
            MailSettingDto mailSetting = new()
            {
                EmailAddress = MailSetting.EmailAddress,
                Username = MailSetting.Username,
                Password = MailSetting.Password,
                SmtpServer = MailSetting.SmtpServer,
                EmailSmtpPort = MailSetting.EmailSmtpPort,
                SmtpTimeOut = MailSetting.SmtpTimeOut
            };
            return (mailDto, mailSetting);
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
