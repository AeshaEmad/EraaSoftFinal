// PointsTransaction.cs
using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class PointsTransaction
{
    [Key]
    public int TransId { get; set; }

    [Required]
    public int AccountId { get; set; }

    [Required]
    public int Points { get; set; }

    [Required]
    [RegularExpression("^(Earned|Redeemed|Refunded|Reversed|Expired)$", ErrorMessage = "Invalid transaction type")]
    public string Type { get; set; } = null!;

    [Required]
    [DataType(DataType.DateTime)]
    public DateTime Date { get; set; } = DateTime.Now;

    public string? Description { get; set; }

    public int? BookingId { get; set; }

    // Navigation
    public virtual RewardAccount RewardAccount { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}
