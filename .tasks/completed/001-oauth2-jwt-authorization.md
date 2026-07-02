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

`appsettings.json` carries only non-secret values (`Issuer`, `Audience`, `ExpiryMinutes`, `ResetTokenExpiryMinutes`). A comment documents where the secret must be supplied.

---

### 2. Access Token Expiry + Phase 2 Roadmap — RESOLVED

**Decision:** Access token expiry is **15 minutes**. No refresh token in Phase 1.

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

---

### 4. ClaimsPrincipal Injection — RESOLVED

Controller extracts `userId` via `User.FindFirstValue(ClaimTypes.NameIdentifier)` and passes as `string userId` parameter.

```csharp
[Authorize]
[HttpPost("change-password")]
public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    => Ok(await AuthService.ChangePassword(User.FindFirstValue(ClaimTypes.NameIdentifier), dto));
```

---

### 5. Rate Limiting — RESOLVED

Two named policies. All four public auth endpoints covered.

| Endpoint                         | Policy         | Limit               |
| -------------------------------- | -------------- | ------------------- |
| `POST /api/auth/login`           | `"auth-login"` | 10 req / 1 min / IP |
| `POST /api/auth/register`        | `"auth"`       | 5 req / 1 min / IP  |
| `POST /api/auth/forgot-password` | `"auth"`       | 5 req / 1 min / IP  |
| `POST /api/auth/reset-password`  | `"auth"`       | 5 req / 1 min / IP  |

---

### 6. EF Migration — RESOLVED

Migration is explicit sub-task 3a with documented rollback.

---

### 7. Password Reset Token Hashing — RESOLVED

Store `SHA-256(token)` only. Raw token in email only.

1. `rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`
2. `tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))`
3. Persist `TokenHash`; email `rawToken`
4. On reset: recompute SHA256, query by hash

---

### 8. Expiring Previous Reset Tokens — RESOLVED

- **ForgotPassword:** revoke all active tokens for user → insert new → single commit
- **ResetPassword:** mark consumed token used → revoke remaining → single commit

---

### 9. Password Policy — RESOLVED

`IValidatorHelper.ValidatePasswordPolicy(string password)` → `(bool IsValid, string ErrorMessage)`.
Min 8 chars / uppercase / lowercase / digit / special char. Called in Register, ChangePassword, ResetPassword.

---

## Sub-Tasks

| #   | Description                                                                                                                                                                  | Status  |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| 1   | **JWT + Rate Limiting Infrastructure** — NuGet, `JwtSettings` model, `appsettings.json` block, JWT auth middleware, `AddRateLimiter` with `"auth"` + `"auth-login"` policies | ✅ done |
| 1a  | **Password Policy Validator** — `ValidatePasswordPolicy` on `IValidatorHelper` + impl                                                                                        | ✅ done |
| 2   | **`IJwtTokenGenerator` / `JwtTokenGenerator`**                                                                                                                               | ✅ done |
| 3   | **`PasswordResetToken` entity + EF config + repository + UoW wiring**                                                                                                        | ✅ done |
| 3a  | **EF Migration** — run, inspect, document rollback                                                                                                                           | ✅ done |
| 4   | **Auth DTOs**                                                                                                                                                                | ✅ done |
| 5   | **`IAuthService` interface**                                                                                                                                                 | ✅ done |
| 6   | **`AuthService.Register`**                                                                                                                                                   | ✅ done |
| 7   | **`AuthService.Login`**                                                                                                                                                      | ✅ done |
| 8   | **`AuthService.ChangePassword`**                                                                                                                                             | ✅ done |
| 9   | **`AuthService.ForgotPassword`**                                                                                                                                             | ✅ done |
| 10  | **`AuthService.ResetPassword`**                                                                                                                                              | ✅ done |
| 11  | **`AuthController`**                                                                                                                                                         | ✅ done |
| 12  | **DI wiring + `[Authorize]` on `UserController`**                                                                                                                            | ✅ done |
| 13  | **Localization keys**                                                                                                                                                        | ✅ done |

---

## Approval Log

| Sub-Task | Approved By | Date       |
| -------- | ----------- | ---------- |
| Plan     | User        | 2026-06-03 |
