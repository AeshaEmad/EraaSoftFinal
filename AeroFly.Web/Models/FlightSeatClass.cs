using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class FlightSeatClass
{
    [Required]
    public int FlightId { get; set; }

    [Required]
    public int ClassId { get; set; }

    [Required]
    [Range(0, 300, ErrorMessage = "Available seats must be between 0 and 300")]
    [Display(Name = "Available Seats")]
    public int AvailableSeats { get; set; }

    [Required]
    [DataType(DataType.Currency)]
    [Display(Name = "Final Price")]
    public decimal FinalPrice { get; set; }

    // Navigation
    public virtual Flight Flight { get; set; } = null!;
    public virtual SeatClass SeatClass { get; set; } = null!;
    public virtual ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();
}