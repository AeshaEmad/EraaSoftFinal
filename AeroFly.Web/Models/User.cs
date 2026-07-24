using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroFly.Web.Models;

public class User
{
    [Key]
    public int UserId { get; set; }

    [Required(ErrorMessage = "First name is required")]
    [Display(Name = "First Name")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters")]
    public string FName { get; set; } = null!;

    [Required(ErrorMessage = "Last name is required")]
    [Display(Name = "Last Name")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters")]
    public string LName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = null!;

    [Required]
    public int CreatedDay { get; set; }

    [Required]
    public int CreatedMonth { get; set; }

    [Required]
    public int CreatedYear { get; set; }

    // AUTH FIELDS

    // Email Confirmation
    public bool EmailConfirmed { get; set; } = false;
    public string? EmailConfirmToken { get; set; }
    public DateTime? EmailConfirmTokenExpiry { get; set; }

    // OTP for Password Reset
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public bool OtpVerified { get; set; } = false;

    // Password Reset Token
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    //ACCOUNT SECURITY FIELDS

    /// <summary>
    /// Number of failed login attempts
    /// </summary>
    public int AccessFailedCount { get; set; } = 0;

    /// <summary>
    /// DateTime when account lockout expires (null = not locked)
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// Last successful login date
    /// </summary>
    public DateTime? LastLoginDate { get; set; }

    [Required]
    [StringLength(64)]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Account creation date (full datetime)
    /// </summary>
    [NotMapped]
    public DateTime CreatedDate => new DateTime(CreatedYear, CreatedMonth, CreatedDay);

    /// <summary>
    /// Checks if account is currently locked
    /// </summary>
    [NotMapped]
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;

    /// <summary>
    /// Checks if account can attempt login (not locked or lockout expired)
    /// </summary>
    [NotMapped]
    public bool CanAttemptLogin => !LockoutEnd.HasValue || LockoutEnd.Value <= DateTime.UtcNow;

    // ===== NAVIGATION PROPERTIES =====
    public virtual Admin? Admin { get; set; }
    public virtual RewardAccount? RewardAccount { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    // ===== COMPUTED PROPERTIES =====
    [Display(Name = "Full Name")]
    public string FullName => $"{FName} {LName}";

    [Display(Name = "Account Age")]
    public string AccountAge
    {
        get
        {
            var age = DateTime.Now - CreatedDate;
            if (age.Days < 30)
                return $"{age.Days} days";
            if (age.Days < 365)
                return $"{age.Days / 30} months";
            return $"{age.Days / 365} years";
        }
    }

    /// <summary>
    /// Resets all security-related fields (used after successful password reset)
    /// </summary>
    public void ResetSecurityTokens()
    {
        OtpCode = null;
        OtpExpiry = null;
        OtpVerified = false;
        ResetPasswordToken = null;
        ResetPasswordTokenExpiry = null;
    }

    /// <summary>
    /// Resets lockout and failed attempts (used after successful login)
    /// </summary>
    public void ResetLockout()
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
    }

    /// <summary>
    /// Records a failed login attempt
    /// </summary>
    public void RecordFailedAttempt(int maxAttempts = 5, int lockoutMinutes = 15)
    {
        AccessFailedCount++;
        if (AccessFailedCount >= maxAttempts)
        {
            LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutMinutes);
        }
    }

    /// <summary>
    /// Checks if email confirmation token is valid
    /// </summary>
    public bool IsEmailConfirmTokenValid()
    {
        return !string.IsNullOrEmpty(EmailConfirmToken) &&
               EmailConfirmTokenExpiry.HasValue &&
               EmailConfirmTokenExpiry.Value > DateTime.Now;
    }

    /// <summary>
    /// Checks if OTP is valid
    /// </summary>
    public bool IsOtpValid(string otpCode)
    {
        return !string.IsNullOrEmpty(OtpCode) &&
               OtpCode == otpCode &&
               OtpExpiry.HasValue &&
               OtpExpiry.Value > DateTime.Now;
    }

    /// <summary>
    /// Checks if reset password token is valid
    /// </summary>
    public bool IsResetTokenValid(string token)
    {
        return !string.IsNullOrEmpty(ResetPasswordToken) &&
               ResetPasswordToken == token &&
               ResetPasswordTokenExpiry.HasValue &&
               ResetPasswordTokenExpiry.Value > DateTime.Now;
    }
}
