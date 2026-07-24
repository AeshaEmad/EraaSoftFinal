using AeroFly.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using AeroFly.Web.Data;
using AeroFly.Web.Repository;
using AeroFly.Web.Repository.IRepository;
using AeroFly.Web.Services;
using Rotativa.AspNetCore;
using Microsoft.AspNetCore.Identity;
using AeroFly.Web.Models;
using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

// ADD SERVICES
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource));
    });

// Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var databaseProvider = builder.Configuration["DatabaseProvider"];

    if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var localConnection = builder.Configuration.GetConnectionString("LocalConnection")
            ?? "Data Source=/tmp/aerofly-local.db";
        options.UseSqlite(localConnection);
    }
    else
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Dependency Injection
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IStripeService, StripeService>(); // Register Stripe Service
builder.Services.AddScoped<IBookingWorkflowService, BookingWorkflowService>();
builder.Services.AddHostedService<SeatHoldCleanupService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IPasswordHasher<User>, BcryptPasswordHasher>();

// AUTHENTICATION
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
        options.Cookie.Name = "AeroFly.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.ReturnUrlParameter = "returnUrl";
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = context.Principal?.FindFirstValue("SecurityStamp");
            if (!int.TryParse(userIdValue, out var userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users
                .Include(u => u.Admin)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);
            var currentRole = user?.Admin?.AdminLevel ?? "User";
            var cookieRole = context.Principal?.FindFirstValue(ClaimTypes.Role);

            if (user == null ||
                user.IsLockedOut ||
                !user.EmailConfirmed ||
                stamp != user.SecurityStamp ||
                cookieRole != currentRole)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Staff", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Moderator"));
    options.AddPolicy("AdminOperations", policy =>
        policy.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));
});

// APP BUILD
var app = builder.Build();

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("ar")
};

// ERROR HANDLING
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// MIDDLEWARE
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var mustChangePassword =
        context.User.FindFirstValue("MustChangePassword") == bool.TrueString;
    var allowedPath = context.Request.Path.StartsWithSegments(
                          "/Identity/Account/ChangePassword") ||
                      context.Request.Path.StartsWithSegments(
                          "/Identity/Account/Logout");
    if (mustChangePassword && !allowedPath)
    {
        context.Response.Redirect("/Identity/Account/ChangePassword");
        return;
    }

    await next();
});
app.UseAuthorization();

// ROUTE CONFIGURATION
// Route for Areas (Admin, Identity, User)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Default route - goes to User Area Home page
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "User", controller = "Home", action = "Index" });

// DATABASE INITIALIZER
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

// ROTATIVA
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");
app.UseRotativa();

// RUN
app.Run();
