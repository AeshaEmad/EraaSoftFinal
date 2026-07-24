using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class Passenger
{
    [Key]
    public int PassengerId { get; set; }

    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Full name must be between 3 and 100 characters")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Passport number is required")]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "Passport number must be between 6 and 20 characters")]
    [Display(Name = "Passport Number")]
    public string PassportNumber { get; set; } = null!;

    [Required]
    [Range(0, 120, ErrorMessage = "Age must be between 0 and 120")]
    public int Age { get; set; }

    [Required]
    public int BookingId { get; set; }

    [Required]
    public int FlightId { get; set; }

    [Required]
    public int ClassId { get; set; }

    // Navigation
    public virtual Booking Booking { get; set; } = null!;
    public virtual FlightSeatClass FlightSeatClass { get; set; } = null!;
    public virtual Ticket? Ticket { get; set; }
}