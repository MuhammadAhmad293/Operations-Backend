# Style Guide: notification

## Unique Conventions

### Two-DTO Method Signature
`IMailSender.SendMail` always takes two separate DTOs — message content and SMTP settings — rather than a single combined DTO:
```csharp
Task<bool> SendMail(MailDto mailDto, MailSettingDto settingDto);
```

### Tuple Return from PrepareMailDtos
The service method that builds both DTOs returns them as a C# value tuple:
```csharp
private (MailDto, MailSettingDto) PrepareMailDtos(Mail mail) { ... }
// Called as:
(MailDto mailDto, MailSettingDto mailSetting) = PrepareMailDtos(mail);
```

### Logging Over Throwing
`MailSender` logs all errors and returns `false` rather than throwing:
```csharp
catch (Exception exception)
{
    Logger.LogInformation(exception, "SendMail");
}
return result; // false
```

### Recipient Validation via IValidatorHelper
Email addresses are validated before sending via `IValidatorHelper.ValidateEmail`, which returns a `Dictionary<string, bool>`. Invalid addresses are filtered out and logged, not rejected as hard errors.

### Mail Entity Persistence is Caller's Responsibility
`IMailSender` only sends — it does not persist the `Mail` entity. The service layer creates the entity, sends the email, then stages the entity on the repository before committing.
