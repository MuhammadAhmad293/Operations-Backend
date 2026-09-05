using Common.Dto;
using Common.HijriCalendar;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Zakat;
using Meezan.IRepositories.UnitOfWork;
using Meezan.IServices.IService;
using Meezan.Services.Base;
using Meezan.Services.CustomExceptions;
using Meezan.Services.Localization;
using Meezan.Services.ZakatEngine;
using MapsterMapper;

namespace Meezan.Services.ZakatService
{
    public class ZakatService : BaseService, IZakatService
    {
        private const string GoldCurrencyCode = "GOLD";

        private IRateService RateService { get; }
        private IZakatPotCalculator PotCalculator { get; }
        private IZakatEngine ZakatEngine { get; }
        private IHijriCalendarHelper HijriCalendarHelper { get; }

        public ZakatService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localization,
            IRateService rateService, IZakatPotCalculator potCalculator, IZakatEngine zakatEngine,
            IHijriCalendarHelper hijriCalendarHelper)
            : base(unitOfWork, mapper, localization)
        {
            RateService = rateService;
            PotCalculator = potCalculator;
            ZakatEngine = zakatEngine;
            HijriCalendarHelper = hijriCalendarHelper;
        }

        // UC-21: pot, nisab status, the running hawl's progress + projection, and total
        // outstanding debt across every Due cycle. Every gram figure is also converted to the
        // account's current base currency at the current gold price (Round-2 refinement) — grams
        // are the stored truth, currency is a read-time-only display conversion.
        public async Task<ResponseDto<ZakatStatusDto>> GetStatus(string? userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<ZakatStatusDto> response = new ResponseDto<ZakatStatusDto>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            decimal pot = await PotCalculator.ComputePotGoldGramsAsync(account.Id, cancellationToken);
            decimal goldToBase = await RateService.GetLatestAsync(GoldCurrencyCode, account.BaseCurrencyCode, cancellationToken);
            bool nisabReached = pot >= ZakatPotCalculator.NisabGoldGrams;

            ZakatCycle? activeCycle = await UnitOfWork.ZakatCycleRepository.GetCurrentActiveByAccountAsync(account.Id);

            ZakatActiveCycleDto? activeCycleDto = null;
            if (activeCycle is not null)
            {
                decimal projectedDue = pot * ZakatPotCalculator.ZakatRate;
                activeCycleDto = new ZakatActiveCycleDto
                {
                    HawlStartHijri = activeCycle.HawlStartHijri,
                    HawlDueHijri = activeCycle.HawlDueHijri,
                    ProjectedDueGoldGrams = projectedDue,
                    ProjectedDueBaseCurrencyValue = projectedDue * goldToBase,
                };
            }

            List<ZakatCycle> dueCycles = await UnitOfWork.ZakatCycleRepository.GetDueByAccountAsync(account.Id);
            decimal totalOutstanding = 0m;
            foreach (ZakatCycle cycle in dueCycles)
            {
                (decimal PaidViaExpenses, decimal Remaining) payment = await ComputePaymentAsync(cycle, cancellationToken);
                totalOutstanding += payment.Remaining;
            }

            ZakatStatusDto status = new()
            {
                PotGoldGrams = pot,
                PotBaseCurrencyValue = pot * goldToBase,
                NisabReached = nisabReached,
                Message = !nisabReached && activeCycle is null ? Localization.NisabNotReached : null,
                ActiveCycle = activeCycleDto,
                TotalOutstandingGoldGrams = totalOutstanding,
                TotalOutstandingBaseCurrencyValue = totalOutstanding * goldToBase,
            };

            return response.GetSuccessResponse(status);
        }

        // Full cycle history (every status) for the Zakat page list.
        public async Task<ResponseDto<List<ZakatCycleDto>>> GetCycles(string? userId, CancellationToken cancellationToken = default)
        {
            ResponseDto<List<ZakatCycleDto>> response = new ResponseDto<List<ZakatCycleDto>>().GetErrorResponse();

            Account account = await GetAccountByUserIdAsync(userId);

            decimal goldToBase = await RateService.GetLatestAsync(GoldCurrencyCode, account.BaseCurrencyCode, cancellationToken);

            List<ZakatCycle> cycles = await UnitOfWork.ZakatCycleRepository.GetHistoryByAccountAsync(account.Id);

            List<ZakatCycleDto> dtos = new(cycles.Count);
            foreach (ZakatCycle cycle in cycles)
            {
                (decimal PaidViaExpenses, decimal Remaining) payment = await ComputePaymentAsync(cycle, cancellationToken);
                decimal? remaining = cycle.ZakatDueGoldGrams.HasValue ? payment.Remaining : null;

                dtos.Add(new ZakatCycleDto
                {
                    Id = cycle.Id,
                    HawlStartHijri = cycle.HawlStartHijri,
                    HawlDueHijri = cycle.HawlDueHijri,
                    Status = cycle.Status.ToString(),
                    DueGoldGrams = cycle.ZakatDueGoldGrams,
                    PaidViaExpensesGoldGrams = payment.PaidViaExpenses,
                    ExternalPaidGoldGrams = cycle.ExternalPaidGoldGrams ?? 0m,
                    RemainingGoldGrams = remaining,
                    DueBaseCurrencyValue = cycle.ZakatDueGoldGrams.HasValue ? cycle.ZakatDueGoldGrams.Value * goldToBase : null,
                    RemainingBaseCurrencyValue = remaining.HasValue ? remaining.Value * goldToBase : null,
                });
            }

            return response.GetSuccessResponse(dtos);
        }

        // UC-24 (revised): pays some or all of a Due cycle's remaining debt from a user-chosen
        // wallet. Built directly here rather than via ITransactionService.Add — see sub-task 1's
        // design note: Add's shared ValidateAndResolveAsync/ResolveConversion unconditionally
        // discards any caller-supplied ExchangeRate/ConvertedAmount for a non-transfer (every
        // Expense has toWallet == null), which would silently drop exactly the audit trail this
        // feature needs to store.
        public async Task<ResponseDto<EmptyResponseDto>> Pay(string? userId, PayZakatDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (request is null)
                throw new InvalidRequestException(Localization.InvalidRequest);

            if (request.Amount.HasValue && request.Amount.Value <= 0)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            ZakatCycle cycle = await UnitOfWork.ZakatCycleRepository.FirstOrDefaultAsync(z => z.Id == request.CycleId && z.AccountId == account.Id)
                ?? throw new ObjectNotFoundException(Localization.NoDataFound);

            if (cycle.Status != ZakatCycleStatus.Due)
                throw new InvalidRequestException(Localization.ZakatCycleNotDue);

            Wallet wallet = await UnitOfWork.WalletRepository.FirstOrDefaultAsync(w => w.Id == request.WalletId && w.AccountId == account.Id && !w.IsDeleted)
                ?? throw new ObjectNotFoundException(Localization.WalletNotFound);

            Category zakatCategory = await UnitOfWork.CategoryRepository.FirstOrDefaultAsync(c => c.AccountId == account.Id && c.SystemPurpose == CategorySystemPurpose.Zakat)
                ?? throw new ObjectNotFoundException(Localization.CategoryNotFound);

            // Default amount: the cycle's full remaining debt, converted to base currency at the
            // current price — lets the user pay it off in one call without doing the math.
            decimal baseCurrencyAmount;
            if (request.Amount.HasValue)
            {
                baseCurrencyAmount = request.Amount.Value;
            }
            else
            {
                (decimal PaidViaExpenses, decimal Remaining) payment = await ComputePaymentAsync(cycle, cancellationToken);
                decimal goldToBase = await RateService.GetLatestAsync(GoldCurrencyCode, account.BaseCurrencyCode, cancellationToken);
                baseCurrencyAmount = payment.Remaining * goldToBase;
            }

            // BR-10's pattern, reused for a same-account expense: Amount is what's actually
            // debited from the wallet (wallet currency, non-negotiable — every balance
            // calculation assumes this), ExchangeRate is wallet→base, ConvertedAmount is the
            // base-currency audit value (== the input baseCurrencyAmount exactly, not
            // recomputed, so it can never drift from what the user actually specified/defaulted to).
            decimal walletAmount;
            decimal? exchangeRate = null;
            decimal? convertedAmount = null;
            if (string.Equals(wallet.CurrencyCode, account.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                walletAmount = baseCurrencyAmount;
            }
            else
            {
                decimal walletToBaseRate = await RateService.GetLatestAsync(wallet.CurrencyCode, account.BaseCurrencyCode, cancellationToken);
                walletAmount = baseCurrencyAmount / walletToBaseRate;
                exchangeRate = walletToBaseRate;
                convertedAmount = baseCurrencyAmount;
            }

            // BR-03: a GOLD wallet's paid amount is already a pure-24K-gram-equivalent value (no
            // physical karat was selected for this payment), so Karat defaults to 24 rather than
            // going through the general ComputeKarat resolution TransactionService uses.
            bool isGoldWallet = string.Equals(wallet.CurrencyCode, GoldCurrencyCode, StringComparison.OrdinalIgnoreCase);
            int? karat = isGoldWallet ? 24 : null;
            decimal? pureGoldGrams = isGoldWallet ? walletAmount : null;

            // Round-2 refinement: the actual wallet-currency amount paid, converted to pure-gold
            // grams at *today's* price and stored once — RecomputeCyclePaymentAsync only ever
            // sums this stored figure, never re-fetches a rate.
            decimal walletToGoldRate = await RateService.GetLatestAsync(wallet.CurrencyCode, GoldCurrencyCode, cancellationToken);
            decimal zakatGoldGrams = walletAmount * walletToGoldRate;

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            Transaction transaction = new()
            {
                AccountId = account.Id,
                Type = TransactionType.Expense,
                DateGregorian = today,
                DateHijri = HijriCalendarHelper.ToHijriString(today),
                Time = TimeOnly.FromDateTime(DateTime.UtcNow),
                Amount = walletAmount,
                WalletId = wallet.Id,
                CategoryId = zakatCategory.Id,
                Karat = karat,
                PureGoldGrams = pureGoldGrams,
                ExchangeRate = exchangeRate,
                ConvertedAmount = convertedAmount,
                ZakatCycleId = cycle.Id,
                ZakatGoldGrams = zakatGoldGrams,
            };

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                UnitOfWork.TransactionRepository.Create(transaction);

                // Same two hooks TransactionService.Add/Update already call for any
                // balance-affecting write: the generic hawl re-evaluation (this payment debits a
                // wallet, which could push a *different*, currently-Active cycle below nisab),
                // then the payment-specific transition for the cycle just paid.
                await ZakatEngine.ReevaluateAsync(account.Id, cancellationToken);
                await ZakatEngine.RecomputeCyclePaymentAsync(cycle.Id, cancellationToken);
            }, cancellationToken);

            return result > default(int)
                ? response.GetSuccessResponse(Localization.ZakatPaid)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        // UC-24 (revised) / BR-20: "I paid outside Meezan" — no wallet touched, no Transaction
        // created. Only allowed on a Due cycle (any of the possibly-several simultaneous ones).
        // Amount is entered in the account's current base currency and converted to pure-gold
        // grams once, at today's price, then accumulated into ExternalPaidGoldGrams — mirrors
        // Transaction.ZakatGoldGrams's "store once, never revalue" rule from Pay.
        public async Task<ResponseDto<EmptyResponseDto>> PayExternal(string? userId, int cycleId, PayExternalZakatDto request, CancellationToken cancellationToken = default)
        {
            ResponseDto<EmptyResponseDto> response = new ResponseDto<EmptyResponseDto>().GetErrorResponse();

            if (request is null || request.Amount <= 0)
                throw new InvalidRequestException(Localization.InvalidRequest);

            Account account = await GetAccountByUserIdAsync(userId);

            ZakatCycle cycle = await UnitOfWork.ZakatCycleRepository.FirstOrDefaultAsync(z => z.Id == cycleId && z.AccountId == account.Id)
                ?? throw new ObjectNotFoundException(Localization.NoDataFound);

            if (cycle.Status != ZakatCycleStatus.Due)
                throw new InvalidRequestException(Localization.ZakatCycleNotDue);

            decimal baseToGoldRate = await RateService.GetLatestAsync(account.BaseCurrencyCode, GoldCurrencyCode, cancellationToken);
            decimal grams = request.Amount * baseToGoldRate;

            int result = await UnitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                cycle.ExternalPaidGoldGrams = (cycle.ExternalPaidGoldGrams ?? 0m) + grams;
                UnitOfWork.ZakatCycleRepository.Update(cycle);

                await ZakatEngine.RecomputeCyclePaymentAsync(cycle.Id, cancellationToken);
            }, cancellationToken);

            return result > default(int)
                ? response.GetSuccessResponse(Localization.ExternalPaymentRecorded)
                : response.GetErrorResponse(Localization.GeneralError);
        }

        // Round-2 refinement's PaidGoldGrams/RemainingGoldGrams formula — shared by GetStatus's
        // outstanding-debt sum and GetCycles' per-cycle breakdown so the two can never disagree.
        private async Task<(decimal PaidViaExpenses, decimal Remaining)> ComputePaymentAsync(ZakatCycle cycle, CancellationToken cancellationToken)
        {
            decimal paidViaExpenses = await UnitOfWork.TransactionRepository.GetZakatGoldGramsSumByCycleAsync(cycle.Id);
            decimal totalPaid = paidViaExpenses + (cycle.ExternalPaidGoldGrams ?? 0m);
            decimal remaining = Math.Max(0m, (cycle.ZakatDueGoldGrams ?? 0m) - totalPaid);
            return (paidViaExpenses, remaining);
        }
    }
}
