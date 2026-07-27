# 002 – Authentication Service Hardening

## Status

`completed`

## Goal

Improve the reliability, security, maintainability, and transactional consistency of the authentication service without changing its public API.

---

## Sub-Tasks

| # | Description | Status |
|---|-------------|--------|
| 1 | Remove password from email bodies — `AuthService`, `UserService`, `appsettings.json` | ✅ done |
| 2 | Fix commit-before-send ordering in `Register()` and `ForgotPassword()` | ✅ done |
| 3 | Add unique indexes on `User.Email` and `User.UserName` | ✅ done |
| 3a | Apply migration `AddUniqueIndexesOnUser` | ✅ done |
| 4 | Catch `DbUpdateException` in `Register()` → translate to localized unique-constraint errors | ✅ done |
| 5 | Rename `CreateAsyn` → `Create` across `IBaseRepository`, `BaseRepository`, and all call sites | ✅ done |
| 6 | Add `CancellationToken` through full call chain: UoW → AuthService → MailSender → AuthController | ✅ done |
| 7 | Extract `Register()` into `ValidateRegistration()`, `CreateUser()`, `CreateWelcomeMail()` private helpers | ✅ done |
| 8 | Review `Login`, `ChangePassword`, `ResetPassword` — apply CT and ordering consistency | ✅ done |

---

## Modified Files

| File | Change |
|------|--------|
| `Operations.Services/AuthService/AuthService.cs` | Password removal, commit ordering, CT, helper extraction, `DbUpdateException` catch, `Create` rename |
| `Operations.Services/UserService/UserService.cs` | Password removal, commit ordering, `Create` rename |
| `Operations/appsettings.json` | `MailSetting.Body` template — removed password placeholder |
| `Operations.Repositories/EntityConfiguration/UserConfiguration.cs` | Unique indexes on `Email` and `UserName` |
| `Operations.Repositories/Migrations/20260702180520_AddUniqueIndexesOnUser.cs` | EF migration |
| `Operations.IRepositories/Base/IBaseRepository.cs` | `CreateAsyn` → `Create` |
| `Operations.Repositories/Base/BaseRepository.cs` | `CreateAsyn` → `Create`, `AddAsync` → `Add` |
| `Operations.IRepositories/UnitOfWork/IUnitOfWork.cs` | `CommitAsync(CancellationToken ct = default)` |
| `Operations.Repositories/UnitOfWork/UnitOfWork.cs` | Propagate CT to `SaveChangesAsync` |
| `Operations.IServices/IService/IAuthService.cs` | CT on all 5 method signatures |
| `Common/Notification/Mail/IMailSender.cs` | CT on `SendMail` |
| `Common/Notification/Mail/MailSender.cs` | Propagate CT to `SendMailAsync` |
| `Operations/Controllers/AuthController.cs` | Pass `HttpContext.RequestAborted` to all service calls |

---

## Approval Log

| Sub-Task | Approved By | Date |
|----------|-------------|------|
| Plan | User | 2026-07-02 |
| All | User | 2026-07-02 |
