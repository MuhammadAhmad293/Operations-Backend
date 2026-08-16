using Common.Dto;
using Common.HijriCalendar;
using Meezan.DataModel.Entities;
using Meezan.Dto.DTOs.Notification;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.Localization;
using Meezan.Services.ZakatEngine;
using MapsterMapper;

namespace Meezan.Services.NotificationService
{
    // BR-16/UC-23: the Zakat reminder toast. Purely a read — never mutates a cycle (that's
    // ZakatEngine's job) — so a stale-by-a-few-minutes pot reading is an accepted tradeoff for
    // not re-running the hawl state machine on every login.
    public class NotificationService : BaseService, INotificationService
    {
        private const int ReminderWindowDays = 14;

        private IZakatPotCalculator PotCalculator { get; }
        private IHijriCalendarHelper HijriCalendarHelper { get; }

        public NotificationService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IZakatPotCalculator potCalculator, IHijriCalendarHelper hijriCalendarHelper)
            : base(unitOfWork, mapper, localization)
        {
            PotCalculator = potCalculator;
            HijriCalendarHelper = hijriCalendarHelper;
        }

        public async Task<ResponseDto<List<LoginNotificationDto>>> GetLoginNotifications(string? userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<LoginNotificationDto>> response = new ResponseDto<List<LoginNotificationDto>>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            List<LoginNotificationDto> notifications = new();

            ZakatCycle? activeCycle = await UnitOfWork.ZakatCycleRepository.GetCurrentActiveByAccountAsync(account.Id);
            if (activeCycle is not null)
            {
                string today = HijriCalendarHelper.ToHijriString(DateOnly.FromDateTime(DateTime.UtcNow));
                string reminderStart = HijriCalendarHelper.AddDays(activeCycle.HawlDueHijri, -ReminderWindowDays);

                // BR-16: from 14 days before hawlDueHijri, up to (not including) the due day
                // itself — once today reaches HawlDueHijri, ReevaluateAsync transitions the
                // cycle to Due on the next trigger, and this is no longer the relevant toast.
                bool withinReminderWindow = string.CompareOrdinal(today, reminderStart) >= 0
                    && string.CompareOrdinal(today, activeCycle.HawlDueHijri) < 0;

                if (withinReminderWindow)
                {
                    // "Trending Due" — only remind if the pot is still above nisab, i.e. still on
                    // track to actually become Due rather than Broken.
                    decimal pot = await PotCalculator.ComputePotGoldGramsAsync(account.Id, cancellationToken);
                    if (pot >= ZakatPotCalculator.NisabGoldGrams)
                    {
                        notifications.Add(new LoginNotificationDto
                        {
                            Type = "ZakatReminder",
                            Message = Localization.ZakatReminderToast,
                        });
                    }
                }
            }

            return response.GetSuccessResponse(notifications);
        }
    }
}
