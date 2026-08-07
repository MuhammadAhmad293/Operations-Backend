using Meezan.DataModel.Entities;

namespace Meezan.Services.Email
{
    public interface ISmtpEmailSender
    {
        Task SendAsync(Mail mail, CancellationToken cancellationToken = default);
    }
}