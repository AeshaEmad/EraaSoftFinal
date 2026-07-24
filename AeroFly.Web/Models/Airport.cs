using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class Airport
{
    [Key]
    public int AirportId { get; set; }

    [Required(ErrorMessage = "Airport name is required")]
    [StringLength(100, ErrorMessage = "Airport name cannot exceed 100 characters")]
    [Display(Name = "Airport Name")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "City is required")]
    [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
    public string City { get; set; } = null!;

    [Required(ErrorMessage = "Country is required")]
    [StringLength(50, ErrorMessage = "Country cannot exceed 50 characters")]
    public string Country { get; set; } = null!;

    [Required(ErrorMessage = "Latitude is required")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double Latitude { get; set; }

    [Required(ErrorMessage = "Longitude is required")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "IATA Code is required")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "IATA code must be exactly 3 characters")]
    [Display(Name = "IATA Code")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "IATA code must be 3 uppercase letters")]
    public string IataCode { get; set; } = null!;

    // Navigation Properties
    public virtual ICollection<Flight> DepartureFlights { get; set; } = new List<Flight>();
    public virtual ICollection<Flight> ArrivalFlights { get; set; } = new List<Flight>();
}