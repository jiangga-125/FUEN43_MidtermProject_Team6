using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoop.Models
{
    public class MailJobRecipient
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long MailJobRecipientId { get; set; }

        [Required]
        public long MailJobId { get; set; }

        [ForeignKey(nameof(MailJobId))]
        public MailJob MailJob { get; set; } = null!;

        [Required, MaxLength(320)]
        public string RecipientEmail { get; set; } = "";

        [MaxLength(200)]
        public string? RecipientName { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending / Sent / Failed

        public DateTime? SentAt { get; set; }
        [MaxLength(1000)]
        public string? Error { get; set; }
    }
}
