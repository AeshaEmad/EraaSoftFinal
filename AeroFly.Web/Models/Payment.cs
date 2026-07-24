using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class Payment
{
    [Key]
    public int PayId { get; set; }

    [Required]
    public int BookingId { get; set; }

    [Required]
    [DataType(DataType.Currency)]
    [Range(0, 100000, ErrorMessage = "Amount must be between 0 and 100,000")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("^(CreditCard|DebitCard|PayPal|RewardPoints)$", ErrorMessage = "Invalid payment method")]
    [Display(Name = "Payment Method")]
    public string PayMethod { get; set; } = null!;

    [Required]
    [RegularExpression("^(Pending|Completed|Failed|Refunded)$", ErrorMessage = "Invalid payment status")]
    [Display(Name = "Payment Status")]
    public string PayStatus { get; set; } = "Pending";

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Payment Date")]
    public DateTime PayDate { get; set; } = DateTime.Now;

    [Required]
    [MaxLength(100)]
    [Display(Name = "Transaction Reference")]
    public string TransactionRef { get; set; } = Guid.NewGuid().ToString();

    [StringLength(100)]
    public string? StripeRefundId { get; set; }

    [StringLength(30)]
    public string? RefundStatus { get; set; }

    [StringLength(500)]
    public string? RefundFailureReason { get; set; }

    public DateTime? RefundedAt { get; set; }

    // Navigation
    public virtual Booking Booking { get; set; } = null!;
}
