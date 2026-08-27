using System.ComponentModel.DataAnnotations;
using IBS.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IBS.Models.Filpride.ViewModels
{
    public class ServiceInvoiceViewModel : IValidatableObject
    {
        public int ServiceInvoiceId { get; set; }

        public string Type { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public List<SelectListItem> Customers { get; set; } = new();

        public int ServiceId { get; set; }

        public List<SelectListItem> Services { get; set; } = new();

        public DateOnly DueDate { get; set; }

        [StringLength(1000)]
        public string Instructions { get; set; } = string.Empty;

        public DateOnly Period { get; set; }

        public decimal Total { get; set; }

        public decimal Discount { get; set; }

        public ServiceInvoiceCreationMode? CreationMode { get; set; }

        public int DurationInMonths { get; set; }

        public decimal AmountPerMonth { get; set; }

        public List<SelectListItem> DeliveryReceipts { get; set; } = new();

        public int? DeliveryReceiptId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var mode = CreationMode ?? (ServiceInvoiceId > 0 ? ServiceInvoiceCreationMode.Manual : null);

            if (string.IsNullOrWhiteSpace(Type))
            {
                yield return new ValidationResult("The Type is required.", new[] { nameof(Type) });
            }

            if (CustomerId <= 0)
            {
                yield return new ValidationResult("The Customer is required.", new[] { nameof(CustomerId) });
            }

            if (ServiceId <= 0)
            {
                yield return new ValidationResult("The Particulars is required.", new[] { nameof(ServiceId) });
            }

            if (Period == default)
            {
                yield return new ValidationResult("The Period is required.", new[] { nameof(Period) });
            }

            if (Discount < 0)
            {
                yield return new ValidationResult("Discount cannot be negative.", new[] { nameof(Discount) });
            }

            if (mode == ServiceInvoiceCreationMode.Manual)
            {
                if (DueDate == default)
                {
                    yield return new ValidationResult("The Due Date is required.", new[] { nameof(DueDate) });
                }

                if (Total <= 0)
                {
                    yield return new ValidationResult("Total must be greater than zero.", new[] { nameof(Total) });
                }
            }

            if (mode == ServiceInvoiceCreationMode.Automatic)
            {
                if (DurationInMonths <= 0)
                {
                    yield return new ValidationResult("Duration must be greater than zero.", new[] { nameof(DurationInMonths) });
                }

                if (AmountPerMonth <= 0)
                {
                    yield return new ValidationResult("Amount per month must be greater than zero.", new[] { nameof(AmountPerMonth) });
                }
            }
        }
    }
}
