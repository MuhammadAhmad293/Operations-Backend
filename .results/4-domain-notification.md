# Domain Deep Dive: Notification (Email)

## Overview
The notification domain handles transactional email sending and logging. It is implemented as a `Common`-layer abstraction so it can be used by any service without coupling to the main domain.

---

## IMailSender Interface

```csharp
public interface IMailSender
{
    Task<bool> SendMail(MailDto mailDto, MailSettingDto settingDto);
}
```

`MailSender` implements this using `System.Net.Mail.SmtpClient`.

---

## MailSender Implementation

Key behaviours:
1. Validates mail data (subject, body, at least one recipient) before sending — logs errors on failure.
2. Validates email address format for each recipient via `IValidatorHelper.ValidateEmail`; filters out invalid addresses and logs them.
3. Builds a `MailMessage`, adds recipients (To, CC, BCC), attachments, and sends synchronously via `SmtpClient.Send`.
4. Returns `true` on success, `false` on any failure — never throws.

```csharp
SmtpClient mailClient = new(settingDto.SmtpServer, settingDto.EmailSmtpPort)
{
    DeliveryMethod = SmtpDeliveryMethod.Network,
    Credentials = new NetworkCredential(settingDto.Username, settingDto.Password),
    Timeout = settingDto.SmtpTimeOut,
};
mailClient.Send(mailMessage);
```

---

## DTOs Used

**MailDto** — the message to send:
- `Id`, `Subject`, `Body`, `IsBodyHtml`
- `MailTo`, `MailCc`, `MailBcc` — `List<string>`
- `Attachment` — `List<string>` (file paths)

**MailSettingDto** — SMTP connection settings (drawn from `MailSettings` config object):
- `EmailAddress`, `SmtpServer`, `Username`, `Password`, `EmailSmtpPort`, `SmtpTimeOut`

---

## Service Integration Pattern

In `UserService`, email sending and Mail entity persistence are co-ordinated in a single flow:

```csharp
// 1. Build the Mail entity
Mail mail = new()
{
    Subject = MailSetting.Subject,
    To = request.Email,
    Body = string.Format(MailSetting.Body, request.Email, request.Password),
    MailStatusId = (int)MailStatusEnum.New,
    MailTypeId = (int)MailTypeEnum.WelcomeMail,
};

// 2. Prepare DTOs and send
(MailDto mailDto, MailSettingDto mailSetting) = PrepareMailDtos(mail);
await MailSender.SendMail(mailDto, mailSetting);

// 3. Stage Mail entity alongside User entity
UnitOfWork.MailRepository.CreateAsyn(mail);

// 4. Commit both in one transaction
await UnitOfWork.CommitAsync();
```

The `PrepareMailDtos` private method extracts both DTO shapes from the entity + configuration in one call.

---

## Configuration

Mail settings are bound from `appsettings.json` into `MailSettings`:
```json
"MailSetting": {
  "EmailAddress": "...",
  "SmtpServer": "smtp.gmail.com",
  "Username": "...",
  "Password": "...",
  "EmailSmtpPort": 465,
  "SmtpTimeOut": 100000,
  "Subject": "...",
  "Body": "Email: {0} , Password: {1}"
}
```

Registered as Singleton in `Program.cs`:
```csharp
MailSettings MailSetting = new();
builder.Configuration.Bind("MailSetting", MailSetting);
builder.Services.AddSingleton(MailSetting);
```

---

## Key Constraints
- `IMailSender` must always be used — no direct `SmtpClient` instantiation in services.
- Mail entity + business entity are always committed atomically via `UnitOfWork.CommitAsync()`.
- `MailSender.SendMail` swallows exceptions and returns `bool`; callers should not rely on it throwing.
