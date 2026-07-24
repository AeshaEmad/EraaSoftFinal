using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class SeatClass
{
    [Key]
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Class name is required")]
    [StringLength(20, ErrorMessage = "Class name cannot exceed 20 characters")]
    [Display(Name = "Class Name")]
    public string ClassName { get; set; } = null!;

    [Required(ErrorMessage = "Class multiplier is required")]
    [Range(0.5, 5, ErrorMessage = "Class multiplier must be between 0.5 and 5")]
    [Display(Name = "Price Multiplier")]
    public decimal ClassMultiplier { get; set; }

    // Navigation
    public virtual ICollection<FlightSeatClass> FlightSeatClasses { get; set; } = new List<FlightSeatClass>();
}