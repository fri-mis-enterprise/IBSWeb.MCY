using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IBS.Models.Filpride.Integrated;
using IBS.Models.Filpride.MasterFile;

namespace IBS.Models.Filpride.AccountsReceivable
{
    public class FilprideRecurringServiceInvoice : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RecurringServiceInvoiceId { get; set; }

        [Required]
        [StringLength(13)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Company { get; set; } = string.Empty;

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public FilprideCustomer? Customer { get; set; }

        [Required]
        public int ServiceId { get; set; }

        [ForeignKey(nameof(ServiceId))]
        public FilprideService? Service { get; set; }

        [StringLength(1000)]
        public string Instructions
        {
            get => _instructions;
            set => _instructions = value.Trim();
        }

        private string _instructions = string.Empty;

        [Required]
        [Column(TypeName = "date")]
        public DateOnly StartPeriod { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateOnly EndPeriod { get; set; }

        [Column(TypeName = "date")]
        public DateOnly? NextRunPeriod { get; set; }

        public int DurationInMonths { get; set; }

        public int GeneratedCount { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal AmountPerMonth { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
