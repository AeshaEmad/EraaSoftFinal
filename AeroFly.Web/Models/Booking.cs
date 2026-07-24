using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class Booking
{
    [Key]
    public int BookingId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int FlightId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Booking Date")]
    public DateTime BookingDate { get; set; } = DateTime.Now;

    [Required]
    [RegularExpression("^(Pending|Confirmed|Cancelled|Completed)$", ErrorMessage = "Invalid status")]
    public string Status { get; set; } = "Pending";

    [Required]
    [DataType(DataType.Currency)]
    [Display(Name = "Total Price")]
    public decimal TotalPrice { get; set; }

    [Display(Name = "Discount Applied")]
    public bool DiscountApplied { get; set; } = false;

    [Display(Name = "Points Used")]
    public int PointsUsed { get; set; } = 0;

    [Display(Name = "Seat hold expires")]
    public DateTime? SeatHoldExpiresAt { get; set; }

    public bool SeatsReserved { get; set; }

    [Display(Name = "PNR (Booking Reference)")]
    public string PNR => $"AF{BookingId:D6}";

    // Navigation
    public virtual User User { get; set; } = null!;
    public virtual Flight Flight { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public virtual ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
}
