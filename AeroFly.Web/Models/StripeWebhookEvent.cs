using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class StripeWebhookEvent
{
    [Key]
    [StringLength(100)]
    public string EventId { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = null!;

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
