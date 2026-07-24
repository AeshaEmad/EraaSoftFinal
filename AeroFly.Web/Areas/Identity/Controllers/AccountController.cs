using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.Services;
using AeroFly.Web.ViewModels;

namespace AeroFly.Web.Areas.Identity.Controllers;

[Area("Identity")]
public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher<AeroFly.Web.Models.User> _passwordHasher;

    public AccountController(
        ApplicationDbContext db,
        IEmailService emailService,
        IPasswordHasher<AeroFly.Web.Models.User> passwordHasher)
    {
        _db = db;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
    }

    private string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private string GenerateOtp() => RandomNumberGenerator.GetInt32(100000, 999999).ToString();

   
    // 1. REGISTER
   
    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (exists)
        {
            ModelState.AddModelError("Email", "This email is already registered.");
            return View(model);
        }

        var now = DateTime.UtcNow;
        var token = GenerateToken();

        var user = new AeroFly.Web.Models.User
        {
            FName = model.FName,
            LName = model.LName,
            Email = normalizedEmail,
            Password = string.Empty,
            CreatedDay = now.Day,
            CreatedMonth = now.Month,
            CreatedYear = now.Year,
            EmailConfirmed = false,
            EmailConfirmToken = token,
            EmailConfirmTokenExpiry = DateTime.UtcNow.AddHours(24),
        };

        user.Password = _passwordHasher.HashPassword(user, model.Password);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(user).State = EntityState.Detached;
            ModelState.AddModelError("Email", "This email is already registered.");
            return View(model);
        }

        _db.RewardAccounts.Add(new RewardAccount
        {
            UserId = user.UserId,
            PointsBalance = 0
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var confirmLink = Url.Action("ConfirmEmail", "Account",
            new { area = "Identity", token, email = user.Email },
            Request.Scheme)!;

        await _emailService.SendConfirmationEmailAsync(user.Email,
            $"{user.FName} {user.LName}", confirmLink);

        TempData["SuccessMessage"] = "Account created! Please check your email to confirm your account.";
        return RedirectToAction("Login");
    }


    // 2. CONFIRM EMAIL
    
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string token, string email)
    {
        var model = new ConfirmEmailViewModel { Email = email };
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.EmailConfirmToken ?? ""),
                Encoding.UTF8.GetBytes(token ?? "")) ||
            user.EmailConfirmTokenExpiry < DateTime.UtcNow)
        {
            model.IsSuccess = false;
            model.Message = "Invalid or expired confirmation link.";
            return View(model);
        }

        if (user.EmailConfirmed)
        {
            model.IsSuccess = true;
            model.Message = "Your email is already confirmed.";
            return View(model);
        }

        user.EmailConfirmed = true;
        user.EmailConfirmToken = null;
        user.EmailConfirmTokenExpiry = null;
        await _db.SaveChangesAsync();

        var otp = GenerateOtp();
        user.OtpCode = otp;
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
        user.OtpVerified = false;
        await _db.SaveChangesAsync();

        await _emailService.SendOtpEmailAsync(user.Email,
            $"{user.FName} {user.LName}", otp);

        model.IsSuccess = true;
        model.Message = "Email confirmed! We sent an OTP code to complete your registration.";

        return RedirectToAction("ValidateOtp", new { email });
    }

    
    // 3. VALIDATE OTP
  
    [HttpGet]
    public IActionResult ValidateOtp(string email)
    {
        return View(new OtpViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateOtp(OtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user == null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.OtpCode ?? ""),
                Encoding.UTF8.GetBytes(model.OtpCode ?? "")) ||
            user.OtpExpiry < DateTime.UtcNow)
        {
            ModelState.AddModelError("OtpCode", "Invalid or expired OTP code.");
            return View(model);
        }

        user.OtpVerified = true;
        user.OtpCode = null;
        user.OtpExpiry = null;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Verification successful! You can now log in.";
        return RedirectToAction("Login");
    }

    
    // 4. LOGIN (with Lockout)
    
    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Admin)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        // Check if account is locked
        if (user.IsLockedOut)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        // Check password
        var passwordResult = _passwordHasher.VerifyHashedPassword(user, user.Password, model.Password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            user.RecordFailedAttempt();
            await _db.SaveChangesAsync();

            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.Password = _passwordHasher.HashPassword(user, model.Password);
        }

        if (!user.EmailConfirmed)
        {
            ModelState.AddModelError("", "Please confirm your email address first.");
            await _db.SaveChangesAsync();
            return View(model);
        }

        if (!user.OtpVerified)
        {
            ModelState.AddModelError("", "Please complete OTP verification first.");
            await _db.SaveChangesAsync();
            return View(model);
        }

        // Password is correct and all checks passed - reset lockout
        user.ResetLockout();
        user.LastLoginDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var isAdmin = user.Admin != null;
        var role = user.Admin?.AdminLevel ?? "User";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FName),
            new Claim(ClaimTypes.Surname, user.LName),
            new Claim("FullName", $"{user.FName} {user.LName}"),
            new Claim(ClaimTypes.Role, role),
            new Claim("SecurityStamp", user.SecurityStamp),
            new Claim("MustChangePassword", user.MustChangePassword.ToString()),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProps = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProps);

        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(ChangePassword));
        }

        if (isAdmin)
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        return RedirectToAction("Index", "Home", new { area = "User" });
    }

   
    // 5. FORGOT PASSWORD
   
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

        TempData["SuccessMessage"] = "If this email is registered, you will receive a password reset link.";

        if (user != null && user.EmailConfirmed)
        {
            var token = GenerateToken();
            user.ResetPasswordToken = token;
            user.ResetPasswordTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();

            var resetLink = Url.Action("ResetPassword", "Account",
                new { area = "Identity", token, email = user.Email },
                Request.Scheme)!;

            await _emailService.SendResetPasswordEmailAsync(user.Email,
                $"{user.FName} {user.LName}", resetLink);
        }

        return RedirectToAction("Login");
    }


    // 6. RESET PASSWORD
    
    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        return View(new ResetPasswordViewModel { Token = token, Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user == null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.ResetPasswordToken ?? ""),
                Encoding.UTF8.GetBytes(model.Token ?? "")) ||
            user.ResetPasswordTokenExpiry < DateTime.UtcNow)
        {
            ModelState.AddModelError("", "Invalid or expired reset link.");
            return View(model);
        }

        user.Password = _passwordHasher.HashPassword(user, model.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ResetPasswordToken = null;
        user.ResetPasswordTokenExpiry = null;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Password changed successfully! You can now log in.";
        return RedirectToAction("Login");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null ||
            _passwordHasher.VerifyHashedPassword(user, user.Password, model.CurrentPassword) ==
            PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        user.Password = _passwordHasher.HashPassword(user, model.NewPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData["SuccessMessage"] = "Password changed. Please sign in again.";
        return RedirectToAction(nameof(Login));
    }

    
    // 7. LOGOUT
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

   
    // HELPER: Resend OTP
   
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user != null && user.EmailConfirmed)
        {
            var otp = GenerateOtp();
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            await _db.SaveChangesAsync();

            await _emailService.SendOtpEmailAsync(user.Email,
                $"{user.FName} {user.LName}", otp);
        }

        TempData["InfoMessage"] = "OTP code resent to your email.";
        return RedirectToAction("ValidateOtp", new { email });
    }

    // 8. PROFILE
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
        {
            return RedirectToAction("Login");
        }

        return View(new ProfileViewModel
        {
            FName = user.FName,
            LName = user.LName,
            Email = user.Email
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
        {
            return RedirectToAction("Login");
        }

        user.FName = model.FName.Trim();
        user.LName = model.LName.Trim();
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }
}
