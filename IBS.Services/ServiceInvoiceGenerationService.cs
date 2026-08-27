using IBS.DataAccess.Repository.IRepository;
using IBS.Models.Enums;
using IBS.Models.Filpride.AccountsReceivable;
using IBS.Utility.Constants;

namespace IBS.Services
{
    public interface IServiceInvoiceGenerationService
    {
        Task<FilprideServiceInvoice> CreateAsync(ServiceInvoiceGenerationRequest request,
            CancellationToken cancellationToken = default);
    }

    public class ServiceInvoiceGenerationRequest
    {
        public required string Company { get; init; }
        public required string Type { get; init; }
        public int CustomerId { get; init; }
        public int ServiceId { get; init; }
        public DateOnly Period { get; init; }
        public DateOnly DueDate { get; init; }
        public required string Instructions { get; init; }
        public decimal Total { get; init; }
        public decimal Discount { get; init; }
        public required string CreatedBy { get; init; }
        public int? RecurringServiceInvoiceId { get; init; }
    }

    public class ServiceInvoiceGenerationService : IServiceInvoiceGenerationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ServiceInvoiceGenerationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<FilprideServiceInvoice> CreateAsync(ServiceInvoiceGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.FilprideCustomer.GetAsync(
                customer => customer.CustomerId == request.CustomerId && customer.Company == request.Company,
                cancellationToken);
            var service = await _unitOfWork.FilprideService.GetAsync(
                service => service.ServiceId == request.ServiceId && service.Company == request.Company,
                cancellationToken);

            if (customer == null || service == null)
            {
                throw new InvalidOperationException("Customer or service could not be found.");
            }

            var normalizedPeriod = new DateOnly(request.Period.Year, request.Period.Month, 1);
            var isTransactionFee = service.Name == "TRANSACTION FEE";

            var model = new FilprideServiceInvoice
            {
                ServiceInvoiceNo = await _unitOfWork.FilprideServiceInvoice.GenerateCodeAsync(
                    request.Company, request.Type, cancellationToken),
                ServiceId = service.ServiceId,
                ServiceName = service.Name,
                ServicePercent = service.Percent,
                CustomerId = customer.CustomerId,
                CustomerName = customer.CustomerName,
                CustomerAddress = customer.CustomerAddress,
                CustomerBusinessStyle = customer.BusinessStyle,
                CustomerTin = customer.CustomerTin,
                VatType = isTransactionFee ? SD.VatType_Exempt : customer.VatType,
                HasEwt = customer.WithHoldingTax && !isTransactionFee,
                HasWvat = customer.WithHoldingVat && !isTransactionFee,
                CreatedBy = request.CreatedBy,
                Total = request.Total,
                Balance = request.Total,
                Company = request.Company,
                Period = normalizedPeriod,
                Instructions = request.Instructions,
                DueDate = request.DueDate,
                Discount = request.Discount,
                Type = request.Type,
                RecurringServiceInvoiceId = request.RecurringServiceInvoiceId
            };

            await _unitOfWork.FilprideServiceInvoice.AddAsync(model, cancellationToken);
            return model;
        }
    }
}
