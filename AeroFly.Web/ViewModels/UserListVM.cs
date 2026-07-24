using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class UserListVM
{
    public int UserId { get; set; }

    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Display(Name = "First Name")]
    public string FName { get; set; } = null!;

    [Display(Name = "Last Name")]
    public string LName { get; set; } = null!;

    [Display(Name = "Email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Joined")]
    public string JoinedDate => $"{CreatedDay}/{CreatedMonth}/{CreatedYear}";

    public int CreatedDay { get; set; }
    public int CreatedMonth { get; set; }
    public int CreatedYear { get; set; }

    [Display(Name = "Account Age")]
    public string AccountAge
    {
        get
        {
            var createdDate = new DateTime(CreatedYear, CreatedMonth, CreatedDay);
            var age = DateTime.Now - createdDate;
            if (age.Days < 30)
                return $"{age.Days} days";
            if (age.Days < 365)
                return $"{age.Days / 30} months";
            return $"{age.Days / 365} years";
        }
    }

    [Display(Name = "Email Confirmed")]
    public bool EmailConfirmed { get; set; }

    [Display(Name = "OTP Verified")]
    public bool OtpVerified { get; set; }

    [Display(Name = "Is Admin")]
    public bool IsAdmin { get; set; }

    [Display(Name = "Status")]
    public string Status
    {
        get
        {
            if (IsLockedOut) return "Locked";
            if (!EmailConfirmed) return "Unverified";
            return "Active";
        }
    }

    public string StatusColor => Status switch
    {
        "Active" => "success",
        "Locked" => "danger",
        "Unverified" => "warning",
        _ => "secondary"
    };

    [Display(Name = "Locked")]
    public bool IsLockedOut { get; set; }

    [Display(Name = "Lockout End")]
    public DateTime? LockoutEnd { get; set; }

    [Display(Name = "Last Login")]
    public DateTime? LastLoginDate { get; set; }

    [Display(Name = "Failed Attempts")]
    public int AccessFailedCount { get; set; }

    [Display(Name = "Reward Points")]
    public int RewardPoints { get; set; }

    [Display(Name = "Total Bookings")]
    public int TotalBookings { get; set; }

    [Display(Name = "Total Spent")]
    [DataType(DataType.Currency)]
    public decimal TotalSpent { get; set; }
}

public class UserDetailsVM
{
    public int UserId { get; set; }

    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Display(Name = "First Name")]
    public string FName { get; set; } = null!;

    [Display(Name = "Last Name")]
    public string LName { get; set; } = null!;

    [Display(Name = "Email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Joined Date")]
    public string JoinedDate => $"{CreatedDay}/{CreatedMonth}/{CreatedYear}";

    public int CreatedDay { get; set; }
    public int CreatedMonth { get; set; }
    public int CreatedYear { get; set; }

    [Display(Name = "Account Age")]
    public string AccountAge
    {
        get
        {
            var createdDate = new DateTime(CreatedYear, CreatedMonth, CreatedDay);
            var age = DateTime.Now - createdDate;
            if (age.Days < 30)
                return $"{age.Days} days";
            if (age.Days < 365)
                return $"{age.Days / 30} months";
            return $"{age.Days / 365} years";
        }
    }

    [Display(Name = "Email Confirmed")]
    public bool EmailConfirmed { get; set; }

    [Display(Name = "OTP Verified")]
    public bool OtpVerified { get; set; }

    [Display(Name = "Is Admin")]
    public bool IsAdmin { get; set; }

    [Display(Name = "Admin Level")]
    public string? AdminLevel { get; set; }

    [Display(Name = "Status")]
    public string Status
    {
        get
        {
            if (IsLockedOut) return "Locked";
            if (!EmailConfirmed) return "Unverified";
            return "Active";
        }
    }

    public string StatusColor => Status switch
    {
        "Active" => "success",
        "Locked" => "danger",
        "Unverified" => "warning",
        _ => "secondary"
    };

    [Display(Name = "Locked")]
    public bool IsLockedOut { get; set; }

    [Display(Name = "Lockout End")]
    public DateTime? LockoutEnd { get; set; }

    [Display(Name = "Last Login")]
    public DateTime? LastLoginDate { get; set; }

    [Display(Name = "Failed Attempts")]
    public int AccessFailedCount { get; set; }

    [Display(Name = "Reward Points")]
    public int RewardPoints { get; set; }

    [Display(Name = "Total Bookings")]
    public int TotalBookings { get; set; }

    [Display(Name = "Total Spent")]
    [DataType(DataType.Currency)]
    public decimal TotalSpent { get; set; }

    // Recent Bookings
    public List<UserBookingSummaryVM> RecentBookings { get; set; } = new();
}

public class UserBookingSummaryVM
{
    [Display(Name = "PNR")]
    public string PNR => $"AF{BookingId:D6}";

    public int BookingId { get; set; }

    [Display(Name = "Flight")]
    public string FlightNumber { get; set; } = null!;

    [Display(Name = "Route")]
    public string Route => $"{DepartureIata} → {ArrivalIata}";

    public string DepartureIata { get; set; } = null!;
    public string ArrivalIata { get; set; } = null!;

    [Display(Name = "Departure")]
    public DateTime DepartureTime { get; set; }

    [Display(Name = "Booking Date")]
    public DateTime BookingDate { get; set; }

    [Display(Name = "Amount")]
    [DataType(DataType.Currency)]
    public decimal TotalPrice { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = null!;

    public string StatusColor => Status switch
    {
        "Confirmed" => "success",
        "Pending" => "warning",
        "Cancelled" => "danger",
        "Completed" => "info",
        _ => "secondary"
    };
}