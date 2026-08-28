using IBS.DataAccess.Data;
using IBS.DataAccess.Repository.IRepository;
using IBS.Models.Enums;
using IBS.Models.Filpride.AccountsPayable;
using IBS.Models.Filpride.AccountsReceivable;
using IBS.Models.Filpride.Books;
using IBS.Utility.Constants;
using IBS.Utility.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IBS.Services
{
    [DisallowConcurrentExecution]
    public class StartOfTheMonthService : IJob
    {
        private readonly IUnitOfWork _unitOfWork;

        private readonly ILogger<StartOfTheMonthService> _logger;

        private readonly ApplicationDbContext _dbContext;

        private readonly IServiceInvoiceGenerationService _serviceInvoiceGenerationService;

        public StartOfTheMonthService(IUnitOfWork unitOfWork,
            ILogger<StartOfTheMonthService> logger, ApplicationDbContext dbContext,
            IServiceInvoiceGenerationService serviceInvoiceGenerationService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _dbContext = dbContext;
            _serviceInvoiceGenerationService = serviceInvoiceGenerationService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var today = DateOnly.FromDateTime(DateTimeHelper.GetCurrentPhilippineTime());
                var previousMonthDate = today.AddMonths(-1);

                await GetTheUnliftedDrs(previousMonthDate);
                await ProcessAmortization(today);
                await ProcessRecurringServiceInvoices(new DateOnly(today.Year, today.Month, 1));
                await SendNotificationToManagementAccounting(previousMonthDate);
                await SendNotificationToCNC(previousMonthDate);
                await ReverseTheJvEntries();

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task GetTheUnliftedDrs(DateOnly previousMonthDate)
        {
            try
            {
                var hasUnliftedDrs = await _dbContext.FilprideDeliveryReceipts
                    .AnyAsync(x => x.Date.Month == previousMonthDate.Month
                                   && x.Date.Year == previousMonthDate.Year
                                   && !x.HasReceivingReport);

                if (hasUnliftedDrs)
                {
                    var users = await _dbContext.ApplicationUsers
                        .Where(u => u.Department == SD.Department_TradeAndSupply
                                    || u.Department == SD.Department_ManagementAccounting)
                        .Select(u => u.Id)
                        .ToListAsync();

                    var message = $"There are still unlifted reports for {previousMonthDate:MMM yyyy}. " +
                                  $"Please ensure the lifting dates for the remaining DRs are recorded to avoid issues during the month-end closing. " +
                                  $"CC: Management Accounting";

                    await _unitOfWork.Notifications.AddNotificationToMultipleUsersAsync(users, message);

                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting the unlifted DRs for {Date}", previousMonthDate);
                throw;
            }
        }

        private async Task ProcessRecurringServiceInvoices(DateOnly currentPeriod)
        {
            try
            {
                var recurringInvoices = await _dbContext.FilprideRecurringServiceInvoices
                    .Where(invoice => invoice.IsActive &&
                                      invoice.NextRunPeriod != null &&
                                      invoice.NextRunPeriod <= currentPeriod)
                    .OrderBy(invoice => invoice.NextRunPeriod)
                    .ToListAsync();

                if (recurringInvoices.Count == 0)
                {
                    return;
                }

                var recurringInvoiceIds = recurringInvoices
                    .Select(invoice => invoice.RecurringServiceInvoiceId)
                    .ToList();
                var generatedInvoicePeriods = (await _dbContext.FilprideServiceInvoices
                        .Where(invoice => invoice.RecurringServiceInvoiceId.HasValue &&
                                          recurringInvoiceIds.Contains(invoice.RecurringServiceInvoiceId.Value) &&
                                          invoice.Period <= currentPeriod &&
                                          invoice.Status != nameof(Status.Voided))
                        .Select(invoice => new
                        {
                            invoice.RecurringServiceInvoiceId,
                            invoice.Period
                        })
                        .ToListAsync())
                    .Select(invoice => (invoice.RecurringServiceInvoiceId!.Value, invoice.Period))
                    .ToHashSet();

                foreach (var recurringInvoice in recurringInvoices)
                {
                    while (recurringInvoice.IsActive &&
                           recurringInvoice.NextRunPeriod != null &&
                           recurringInvoice.NextRunPeriod <= currentPeriod)
                    {
                        var invoicePeriod = NormalizePeriod(recurringInvoice.NextRunPeriod.Value);

                        if (generatedInvoicePeriods.Add((recurringInvoice.RecurringServiceInvoiceId, invoicePeriod)))
                        {
                            var generatedInvoice = await _serviceInvoiceGenerationService.CreateAsync(
                                new ServiceInvoiceGenerationRequest
                                {
                                    Type = recurringInvoice.Type,
                                    CustomerId = recurringInvoice.CustomerId,
                                    ServiceId = recurringInvoice.ServiceId,
                                    Period = invoicePeriod,
                                    DueDate = GetPeriodEndDate(invoicePeriod),
                                    Instructions = recurringInvoice.Instructions,
                                    Total = recurringInvoice.AmountPerMonth,
                                    Discount = 0,
                                    CreatedBy = "SYSTEM",
                                    RecurringServiceInvoiceId = recurringInvoice.RecurringServiceInvoiceId
                                });

                            await _unitOfWork.FilprideAuditTrail.AddAsync(new FilprideAuditTrail("SYSTEM",
                                $"Generated service invoice# {generatedInvoice.ServiceInvoiceNo} from recurring setup# {recurringInvoice.RecurringServiceInvoiceId}",
                                "Service Invoice"));
                        }

                        recurringInvoice.GeneratedCount = Math.Max(recurringInvoice.GeneratedCount,
                            GetSequenceNumber(recurringInvoice, invoicePeriod));
                        recurringInvoice.IsActive = recurringInvoice.GeneratedCount < recurringInvoice.DurationInMonths;
                        recurringInvoice.NextRunPeriod = recurringInvoice.IsActive
                            ? recurringInvoice.StartPeriod.AddMonths(recurringInvoice.GeneratedCount)
                            : null;
                    }
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing recurring service invoices for {Period}", currentPeriod);
                throw;
            }
        }

        private static int GetSequenceNumber(FilprideRecurringServiceInvoice recurringInvoice,
            DateOnly invoicePeriod)
        {
            return ((invoicePeriod.Year - recurringInvoice.StartPeriod.Year) * 12) +
                   invoicePeriod.Month - recurringInvoice.StartPeriod.Month + 1;
        }

        private static DateOnly NormalizePeriod(DateOnly period)
        {
            return new DateOnly(period.Year, period.Month, 1);
        }

        private static DateOnly GetPeriodEndDate(DateOnly period)
        {
            return NormalizePeriod(period).AddMonths(1).AddDays(-1);
        }

        private async Task ProcessAmortization(DateOnly dateToday)
        {
            try
            {
                var amortizationSetting = await _dbContext.JvAmortizationSettings
                .Include(x => x.JvHeader)
                    .ThenInclude(x => x.Details)
                .Where(x =>
                (x.NextRunDate == null || x.NextRunDate <= dateToday) &&
                x.IsActive &&
                x.JvHeader.PostedBy != null)
                .ToListAsync();

                if (amortizationSetting.Count == 0)
                {
                    return;
                }

                var newJournalVouchers = new List<FilprideJournalVoucherHeader>();

                var groupedAmortizations = amortizationSetting
                    .GroupBy(a => a.JvHeader.Type)
                    .ToList();

                foreach (var group in groupedAmortizations)
                {
                    var baseCode = await _unitOfWork.FilprideJournalVoucher
                        .GenerateCodeAsync(group.Key);

                    var offset = 0;
                    foreach (var amortization in group)
                    {
                        var sourceJv = amortization.JvHeader;

                        if (sourceJv?.Details == null || sourceJv.Details.Count == 0)
                        {
                            throw new InvalidOperationException(
                                $"The source journal voucher for amortization with ID {amortization.JvId} is missing or has no details.");
                        }

                        var generatedCode = IncrementCode(baseCode, offset++);

                        var newHeader = new FilprideJournalVoucherHeader
                        {
                            Type = sourceJv.Type,
                            JournalVoucherHeaderNo = generatedCode,
                            Date = dateToday,
                            References = sourceJv.References,
                            CVId = sourceJv.CVId,
                            Particulars = sourceJv.Particulars,
                            CRNo = sourceJv.CRNo,
                            JVReason = sourceJv.JVReason,
                            CreatedBy = "SYSTEM GENERATED",
                            JvType = nameof(JvType.Amortization),
                            Status = nameof(JvStatus.Pending),
                            Details = sourceJv.Details.Select(detail => new FilprideJournalVoucherDetail
                            {
                                AccountNo = detail.AccountNo,
                                AccountName = detail.AccountName,
                                TransactionNo = detail.TransactionNo,
                                Debit = detail.Debit,
                                Credit = detail.Credit,
                                SubAccountType = detail.SubAccountType,
                                SubAccountId = detail.SubAccountId,
                                SubAccountName = detail.SubAccountName
                            }).ToList()
                        };

                        newJournalVouchers.Add(newHeader);
                        amortization.LastRunDate = dateToday;
                        if (amortization.OccurrenceRemaining > 0)
                        {
                            amortization.OccurrenceRemaining--;
                        }
                        amortization.IsActive = amortization.OccurrenceRemaining > 0;
                        amortization.NextRunDate = amortization.IsActive ? dateToday.AddMonths(1) : null;
                    }
                }

                await _dbContext.FilprideJournalVoucherHeaders.AddRangeAsync(newJournalVouchers);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing amortization for {Date}", dateToday);
                throw;
            }
        }

        private static string IncrementCode(string baseCode, int offset)
        {
            if (baseCode.StartsWith("JVU"))
            {
                var numericPart = baseCode.Substring(3);
                var incremented = long.Parse(numericPart) + offset;
                return "JVU" + incremented.ToString("D9");
            }
            else
            {
                var numericPart = baseCode.Substring(2);
                var incremented = long.Parse(numericPart) + offset;
                return "JV" + incremented.ToString("D10");
            }
        }

        private async Task SendNotificationToManagementAccounting(DateOnly previousMonth)
        {
            var users = await _dbContext.ApplicationUsers
                .Where(u => u.IsActive && u.Department == SD.Department_ManagementAccounting)
                .Select(u => u.Id)
                .ToListAsync();

            var message = $"Kindly generate the journal voucher list for {previousMonth:MMM yyyy}.";

            await _unitOfWork.Notifications.AddNotificationToMultipleUsersAsync(users, message);
        }

        private async Task SendNotificationToCNC(DateOnly previousMonth)
        {
            var users = await _dbContext.ApplicationUsers
                .Where(u => u.IsActive && u.Department == SD.Department_CreditAndCollection)
                .Select(u => u.Id)
                .ToListAsync();

            var message = $"Please ensure the transaction fee is created before the system closes the books for {previousMonth:MMM yyyy}.";

            await _unitOfWork.Notifications.AddNotificationToMultipleUsersAsync(users, message);
        }

        private async Task ReverseTheJvEntries()
        {
            var currentDateTime = DateTimeHelper.GetCurrentPhilippineTime();
                var currentDate = DateOnly.FromDateTime(currentDateTime);

                var journalVouchers = await _dbContext.FilprideJournalVoucherHeaders
                                          .Include(x => x.Details)
                                          .Where(x => x.AutoReverseNextMonth)
                                          .ToListAsync()
                                      ?? throw new InvalidOperationException("Journal voucher auto reverse next month not found.");

                var accountTitlesDto = await _unitOfWork.FilprideJournalVoucher.GetListOfAccountTitleDto();
                var ledgers = new List<FilprideGeneralLedgerBook>();

                foreach (var journalVoucherHeaders in journalVouchers)
                {
                    foreach (var detail in journalVoucherHeaders.Details!)
                    {
                        var account = accountTitlesDto.Find(c => c.AccountNumber == detail.AccountNo)
                                      ?? throw new ArgumentException($"Account title '{detail.AccountNo}' not found.");

                        ledgers.Add(
                            new FilprideGeneralLedgerBook
                            {
                                Date = currentDate,
                                Reference = journalVoucherHeaders.JournalVoucherHeaderNo!,
                                Description = $"Reversal of {journalVoucherHeaders.Particulars}",
                                AccountId = account.AccountId,
                                AccountNo = account.AccountNumber,
                                AccountTitle = account.AccountName,
                                Debit = detail.Credit,
                                Credit = detail.Debit,
                                CreatedBy = journalVoucherHeaders.CreatedBy!,
                                CreatedDate = currentDateTime,
                                SubAccountType = detail.SubAccountType,
                                SubAccountId = detail.SubAccountId,
                                SubAccountName = detail.SubAccountName,
                                ModuleType = nameof(ModuleType.Journal)
                            }
                        );
                    }

                    if (!_unitOfWork.FilprideJournalVoucher.IsJournalEntriesBalanced(ledgers))
                    {
                        throw new ArgumentException("Debit and Credit is not equal, check your entries.");
                    }

                    journalVoucherHeaders.AutoReverseNextMonth = false;
                }
                await _dbContext.FilprideGeneralLedgerBooks.AddRangeAsync(ledgers);
                await _dbContext.SaveChangesAsync();
        }
    }
}
