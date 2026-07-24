using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class Ticket
{
    [Key]
    public int TicketId { get; set; }

    [Required]
    public int BookingId { get; set; }

    [Required]
    public int FlightId { get; set; }

    [Required]
    public int PassengerId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Issue Date")]
    public DateTime IssueDate { get; set; } = DateTime.Now;

    [Required]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Seat number must be between 2 and 10 characters")]
    [Display(Name = "Seat Number")]
    public string SeatNum { get; set; } = null!;

    [Display(Name = "QR Code")]
    public string QrCode { get; set; } = Guid.NewGuid().ToString();

    public bool IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }

    [Display(Name = "Ticket Number")]
    public string TicketNumber => $"AF{TicketId:D8}";

    // Navigation
    public virtual Booking Booking { get; set; } = null!;
    public virtual Flight Flight { get; set; } = null!;
    public virtual Passenger Passenger { get; set; } = null!;
}
