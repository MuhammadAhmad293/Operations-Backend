# 001 – OAuth2 / JWT Authorization

## Status

`completed`

## Goal

Add a complete authentication and authorization layer using JWT Bearer tokens. Users can register, log in, change their password while authenticated, and reset a forgotten password via an emailed link.

---

## Resolved Architecture Decisions

### 1. JWT Secret Storage — RESOLVED

**Decision:** `JwtSettings.Secret` is bound via `IOptions<JwtSettings>` but the secret value is **never stored in `appsettings.json` or committed to source control**.

| Environment | Secret Source                                                                         |
| ----------- | ------------------------------------------------------------------------------------- |
| Local dev   | `dotnet user-secrets set "JwtSettings:Secret" "..."`                                  |
| Production  | Environment variable `JwtSettings__Secret` (double-underscore = colon in .NET config) |

`appsettings.json` carries only non-secret values (`Issuer`, `Audience`, `ExpiryMinutes`, `ResetTokenExpiryMinutes`). A comment documents where the secret must be supplied:

```json
"JwtSettings": {
  "Issuer": "MeezanApi",
  "Audience": "MeezanClient",
  "ExpiryMinutes": 15,
  "ResetTokenExpiryMinutes": 60
  // Secret: set via user-secrets (dev) or env var JwtSettings__Secret (prod). Min 32 chars.
}
```

---

### 2. Access Token Expiry + Phase 2 Roadmap — RESOLVED

**Decision:** Access token expiry is **15 minutes**. No refresh token in Phase 1.

**Rationale:** 15-minute tokens limit the blast radius of a stolen token to an acceptable window.

**Phase 2 scope (task 002 — not in scope here):**

- `RefreshToken` entity + `POST /api/auth/refresh` + `POST /api/auth/logout`
- `jti` claim in access tokens
- Server-side revocation check on `ChangePassword` / `ResetPassword`
- Migrate `PasswordHash` from 50 iterations to ASP.NET Core `PasswordHasher<T>` (600k PBKDF2-SHA256)

---

### 3. Token Revocation / Logout — RESOLVED

**Decision:** Phase 1 has no server-side revocation. Accepted given 15-minute expiry.

| Event            | Old access token       | Behaviour                                |
| ---------------- | ---------------------- | ---------------------------------------- |
| `ChangePassword` | Valid for up to 15 min | Accepted — short window                  |
| `ResetPassword`  | Valid for up to 15 min | Accepted — short window                  |
| Logout           | No server-side action  | Client discards token; expires naturally |

Recorded here so it is not re-discovered as a bug. Phase 2 resolves it.

---

### 4. ClaimsPrincipal Injection — RESOLVED

**Decision:** Do not inject `ClaimsPrincipal` into `AuthService`. Controller extracts `userId` via `User.FindFirstValue(ClaimTypes.NameIdentifier)` and passes it as a `string userId` parameter.

```csharp
// AuthController
[Authorize]
[HttpPost("change-password")]
public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    => Ok(await AuthService.ChangePassword(User.FindFirstValue(ClaimTypes.NameIdentifier), dto));

// IAuthService
Task<ResponseDto<EmptyResponseDto>> ChangePassword(string userId, ChangePasswordDto dto);
```

The existing Transient `ClaimsPrincipal` registration in `Program.cs` is left untouched.

---

### 5. Rate Limiting — RESOLVED

**Decision:** ASP.NET Core built-in `AddRateLimiter`, fixed-window policy, per IP, on all four auth endpoints that accept unauthenticated input.

| Endpoint                         | Limit          |
| -------------------------------- | -------------- |
| `POST /api/auth/login`           | 10 req / 1 min |
| `POST /api/auth/register`        | 5 req / 1 min  |
| `POST /api/auth/forgot-password` | 5 req / 1 min  |
| `POST /api/auth/reset-password`  | 5 req / 1 min  |

Returns HTTP 429 on breach. Applied via `[EnableRateLimiting("auth")]` on individual actions.

---

### 6. EF Migration — RESOLVED

Migration is an explicit sub-task (3a) with documented rollback:

```
Up:       dotnet ef migrations add AddPasswordResetToken --project Meezan.Repositories
Rollback (before apply): dotnet ef migrations remove --project Meezan.Repositories
Rollback (after apply):  dotnet ef database update <previous-migration-name> --project Meezan.Repositories
```

Migration file must be inspected as part of Sub-task 3a approval.

---

### 7. Password Reset Token Hashing — RESOLVED

**Decision:** The database stores `SHA-256(token)` only. The raw token travels exclusively in the email link. This ensures a DB breach does not expose usable reset tokens.

**Flow:**

1. Generate: `rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))` (256 bits entropy)
2. Hash: `tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))`
3. Persist: `PasswordResetToken.TokenHash = tokenHash`
4. Email: send `rawToken` in the reset link query string
5. On reset: recompute `SHA256(receivedRawToken)` → query DB by `TokenHash`

**Entity field:** `TokenHash` (string) — not `Token`. No raw token is ever written to the DB.

`IPasswordResetTokenRepository` must expose two custom query methods beyond `BaseRepository<T>`:

- `Task<PasswordResetToken> GetByTokenHashAsync(string tokenHash)` — find valid token (not expired, not used)
- `Task<List<PasswordResetToken>> GetActiveByUserIdAsync(int userId)` — find all active tokens for a user

---

### 8. Expiring Previous Reset Tokens — RESOLVED

**Decision:** A user may only have one active reset token at a time.

- **ForgotPassword:** Before creating a new `PasswordResetToken`, query all active tokens for that user via `GetActiveByUserIdAsync` and mark each `IsUsed = true`. Then insert the new token. Both updates and insert are committed in a single `CommitAsync()`.
- **ResetPassword:** After a successful password update, mark the consumed token `IsUsed = true` AND revoke all remaining active tokens for that user (same pattern — query + mark + commit).

---

### 9. Password Policy — RESOLVED

**Decision:** A `ValidatePasswordPolicy(string password)` method is added to `IValidatorHelper` (in `Common`). Returns `(bool IsValid, string ErrorMessage)` value tuple.

**Policy:**
| Rule | Requirement |
| ---- | ----------- |
| Length | Minimum 8 characters |
| Uppercase | At least 1 uppercase letter (A–Z) |
| Lowercase | At least 1 lowercase letter (a–z) |
| Digit | At least 1 digit (0–9) |
| Special char | At least 1 of `!@#$%^&*()_+-=[]{}|;':\",./<>?` |

Called in: `AuthService.Register`, `AuthService.ChangePassword` (new password only), `AuthService.ResetPassword`.

New localization key: `PasswordPolicyViolation` — returns the specific rule that failed.

---

## Scope

### New Endpoints (AuthController — `/api/auth/`)

| Method | Route                       | Auth Required | Rate Limited |
| ------ | --------------------------- | ------------- | ------------ |
| POST   | `/api/auth/register`        | No            | ✅ 5/min     |
| POST   | `/api/auth/login`           | No            | ✅ 10/min    |
| POST   | `/api/auth/change-password` | ✅ JWT        | No           |
| POST   | `/api/auth/forgot-password` | No            | ✅ 5/min     |
| POST   | `/api/auth/reset-password`  | No            | ✅ 5/min     |

### Existing Endpoints — Impact

- `UserController` all actions → add `[Authorize]`
- `JobTestController` → no change (infrastructure demo, leave open)

---

## New Files

| File                                                                         | Category              |
| ---------------------------------------------------------------------------- | --------------------- |
| `Meezan.DataModel/Entities/PasswordResetToken.cs`                            | entities              |
| `Meezan.Repositories/EntityConfiguration/PasswordResetTokenConfiguration.cs` | entity-configurations |
| `Meezan.IRepositories/IRepository/IPasswordResetTokenRepository.cs`          | repository-interfaces |
| `Meezan.Repositories/Repository/PasswordResetTokenRepository.cs`             | repositories          |
| `Meezan.Dto/DTOs/Auth/RegisterDto.cs`                                        | dtos                  |
| `Meezan.Dto/DTOs/Auth/LoginDto.cs`                                           | dtos                  |
| `Meezan.Dto/DTOs/Auth/LoginResponseDto.cs`                                   | dtos                  |
| `Meezan.Dto/DTOs/Auth/ChangePasswordDto.cs`                                  | dtos                  |
| `Meezan.Dto/DTOs/Auth/ForgotPasswordDto.cs`                                  | dtos                  |
| `Meezan.Dto/DTOs/Auth/ResetPasswordDto.cs`                                   | dtos                  |
| `Meezan.Services/Auth/IJwtTokenGenerator.cs`                                 | service-interfaces    |
| `Meezan.Services/Auth/JwtTokenGenerator.cs`                                  | services              |
| `Meezan.IServices/IService/IAuthService.cs`                                  | service-interfaces    |
| `Meezan.Services/AuthService/AuthService.cs`                                 | services              |
| `Meezan/Controllers/AuthController.cs`                                       | api-controllers       |
| `Meezan.Services/Setting/JwtSettings.cs`                                     | settings-models       |

## Modified Files

| File                                                                        | Change                                                        |
| --------------------------------------------------------------------------- | ------------------------------------------------------------- |
| `Meezan.Repositories/Context/AppDbContext.cs`                               | Add `DbSet<PasswordResetToken>`                               |
| `Meezan.IRepositories/UnitOfWork/IUnitOfWork.cs`                            | Add `IPasswordResetTokenRepository` property                  |
| `Meezan.Repositories/UnitOfWork/UnitOfWork.cs`                              | Add repository property                                       |
| `Meezan.Services/Resolver/CoreServicesResolver.cs`                          | Register `IAuthService`, `IJwtTokenGenerator`                 |
| `Meezan/Program.cs`                                                         | Add JWT auth middleware, `AddRateLimiter`, bind `JwtSettings` |
| `Meezan/Meezan.csproj`                                                      | Add `Microsoft.AspNetCore.Authentication.JwtBearer`           |
| `Meezan/appsettings.json`                                                   | Add `JwtSettings` block (no secret value)                     |
| `Common/Validator/IValidatorHelper.cs`                                      | Add `ValidatePasswordPolicy(string password)`                 |
| `Common/Validator/ValidatorHelper.cs`                                       | Implement `ValidatePasswordPolicy`                            |
| `Meezan.Services/Localization/LocalizationFileReader/localizationFile.json` | Add auth + policy keys                                        |
| `Meezan.Services/Localization/ILocalizationService.cs`                      | Expose new keys                                               |
| `Meezan.Services/Localization/LocalizationService.cs`                       | Implement new keys                                            |
| `Meezan/Controllers/UserController.cs`                                      | Add `[Authorize]` to all actions                              |

---

## Sub-Tasks

| #   | Description                                                                                                                                                                                                                                                                                                                                                                       | Status     |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| 1   | **JWT + Rate Limiting Infrastructure** — NuGet (`Microsoft.AspNetCore.Authentication.JwtBearer`), `JwtSettings` model, `appsettings.json` block (no secret), user-secrets setup comment, JWT auth middleware + `AddRateLimiter` with `"auth"` policy (4 endpoints, limits per table above) in `Program.cs`                                                                        | ⬜ pending |
| 1a  | **Password Policy Validator** — add `ValidatePasswordPolicy(string password)` to `IValidatorHelper` + `ValidatorHelper`; returns `(bool IsValid, string ErrorMessage)` tuple; enforces 8 chars / uppercase / lowercase / digit / special char                                                                                                                                     | ⬜ pending |
| 2   | **`IJwtTokenGenerator` / `JwtTokenGenerator`** — builds and signs JWT with `sub` (userId), `email`, `unique_name` claims; 15-min expiry from `JwtSettings`                                                                                                                                                                                                                        | ⬜ pending |
| 3   | **`PasswordResetToken` entity + EF config + repository** — entity has `TokenHash` (not raw token), `UserId`, `ExpiresAt`, `IsUsed`; `IEntityTypeConfiguration`; `IPasswordResetTokenRepository` with two custom methods (`GetByTokenHashAsync`, `GetActiveByUserIdAsync`); concrete repository; `IUnitOfWork` + `UnitOfWork` wiring                                               | ⬜ pending |
| 3a  | **EF Migration** — run `AddPasswordResetToken` migration; inspect generated file; document rollback command in task notes                                                                                                                                                                                                                                                         | ⬜ pending |
| 4   | **Auth DTOs** — `RegisterDto`, `LoginDto`, `LoginResponseDto`, `ChangePasswordDto`, `ForgotPasswordDto`, `ResetPasswordDto`                                                                                                                                                                                                                                                       | ⬜ pending |
| 5   | **`IAuthService` interface** — 5 method signatures; `ChangePassword(string userId, ChangePasswordDto dto)`                                                                                                                                                                                                                                                                        | ⬜ pending |
| 6   | **`AuthService.Register`** — validate password policy, check email + username uniqueness, hash password, create User, send welcome email, commit                                                                                                                                                                                                                                  | ⬜ pending |
| 7   | **`AuthService.Login`** — find user by email, `IPasswordHash.ValidatePassword`, generate JWT, return `LoginResponseDto`                                                                                                                                                                                                                                                           | ⬜ pending |
| 8   | **`AuthService.ChangePassword`** — receive `userId`, load user, verify current password via `ValidatePassword`, validate new password policy, hash + update, commit                                                                                                                                                                                                               | ⬜ pending |
| 9   | **`AuthService.ForgotPassword`** — silent success if email not found (prevent user enumeration); revoke all active tokens for user (`GetActiveByUserIdAsync` → mark `IsUsed = true`); generate `rawToken` via `RandomNumberGenerator`, compute `tokenHash = SHA256(rawToken)`; persist `PasswordResetToken`; send email with raw token link; commit all in single `CommitAsync()` | ⬜ pending |
| 10  | **`AuthService.ResetPassword`** — compute `SHA256(receivedToken)`; call `GetByTokenHashAsync`; validate (not null, not expired, not used); validate new password policy; update user password; mark consumed token `IsUsed = true`; revoke all remaining active tokens for user; commit                                                                                           | ⬜ pending |
| 11  | **`AuthController`** — 5 thin endpoints; `ChangePassword` passes `User.FindFirstValue(ClaimTypes.NameIdentifier)`; `[EnableRateLimiting("auth")]` on `register`, `login`, `forgot-password`, `reset-password`                                                                                                                                                                     | ⬜ pending |
| 12  | **DI wiring + endpoint protection** — register `IAuthService` + `IJwtTokenGenerator` in `CoreServicesResolver`; add `[Authorize]` to `UserController`                                                                                                                                                                                                                             | ⬜ pending |
| 13  | **Localization keys** — add all 10 auth + policy keys (en + ar) to `localizationFile.json`, `ILocalizationService`, `LocalizationService`                                                                                                                                                                                                                                         | ⬜ pending |

---

## Localization Keys

| Key                       | English                                                                                                 | Arabic                                                                                          |
| ------------------------- | ------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `EmailAlreadyExists`      | "Email is already registered"                                                                           | "البريد الإلكتروني مسجل مسبقاً"                                                                 |
| `UserNameAlreadyExists`   | "Username is already taken"                                                                             | "اسم المستخدم محجوز مسبقاً"                                                                     |
| `InvalidCredentials`      | "Invalid email or password"                                                                             | "البريد الإلكتروني أو كلمة المرور غير صحيحة"                                                    |
| `InvalidCurrentPassword`  | "Current password is incorrect"                                                                         | "كلمة المرور الحالية غير صحيحة"                                                                 |
| `PasswordResetSent`       | "Password reset link has been sent to your email"                                                       | "تم إرسال رابط إعادة تعيين كلمة المرور إلى بريدك الإلكتروني"                                    |
| `InvalidResetToken`       | "Reset token is invalid or has expired"                                                                 | "رمز إعادة التعيين غير صالح أو منتهي الصلاحية"                                                  |
| `PasswordResetSuccess`    | "Password has been reset successfully"                                                                  | "تم إعادة تعيين كلمة المرور بنجاح"                                                              |
| `RegistrationSuccess`     | "Registration successful"                                                                               | "تم التسجيل بنجاح"                                                                              |
| `PasswordMismatch`        | "Passwords do not match"                                                                                | "كلمتا المرور غير متطابقتان"                                                                    |
| `PasswordPolicyViolation` | "Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character" | "يجب أن تحتوي كلمة المرور على 8 أحرف على الأقل وتتضمن حرفاً كبيراً وصغيراً ورقماً وحرفاً خاصاً" |

---

## Remaining Risks (Documented, Accepted)

| Risk                                       | Accepted?         | Mitigation                                                                |
| ------------------------------------------ | ----------------- | ------------------------------------------------------------------------- |
| Old JWT valid 15 min after password change | ✅ Yes — Phase 1  | Short expiry limits window; Phase 2 adds revocation                       |
| No server-side logout                      | ✅ Yes — Phase 1  | Client discards token; Phase 2 adds refresh token revocation              |
| No CAPTCHA                                 | ✅ Yes — Phase 1  | Rate limiting on all 4 public auth endpoints                              |
| PBKDF2 iterations = 50 in `PasswordHash`   | ⚠️ Known weakness | Below OWASP recommendation. Deferred to Phase 2 / separate hardening task |

---

## Phase 2 Scope (Task 002 — Not In This Task)

- Refresh token + logout + revocation
- `jti` claim + revocation check on password change
- PBKDF2 iteration count hardening

---

## Approval Log

| Sub-Task | Approved By | Date |
| -------- | ----------- | ---- |
