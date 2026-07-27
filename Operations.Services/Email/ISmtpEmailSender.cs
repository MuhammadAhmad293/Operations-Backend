using Operations.DataModel.Entities;

namespace Operations.Services.Email
{
    public interface ISmtpEmailSender
    {
        Task SendAsync(Mail mail, CancellationToken cancellationToken = default);
    }
}