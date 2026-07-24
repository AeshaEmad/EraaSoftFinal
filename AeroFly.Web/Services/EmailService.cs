using AeroFly.Web.Models;
using AeroFly.Web.ViewModels;
using System.Net;
using System.Net.Mail;

namespace AeroFly.Web.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    Task SendConfirmationEmailAsync(string toEmail, string userName, string confirmLink);
    Task SendOtpEmailAsync(string toEmail, string userName, string otpCode);
    Task SendResetPasswordEmailAsync(string toEmail, string userName, string resetLink);
    Task SendBookingConfirmationEmailAsync(string toEmail, string userName, BookingConfirmationVM booking);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpHost = _config["EmailSettings:SmtpHost"]!;
        var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"]!);
        var senderEmail = _config["EmailSettings:SenderEmail"]!;
        var senderPass = _config["EmailSettings:SenderPassword"]!;
        var senderName = _config["EmailSettings:SenderName"]!;

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(senderEmail, senderPass),
            EnableSsl = true
        };

        var mail = new MailMessage
        {
            From = new MailAddress(senderEmail, senderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(toEmail);

        await client.SendMailAsync(mail);
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string userName, string confirmLink)
    {
        var subject = "✈️ Confirm Your Email - AeroFly";
        var body = $@"
        <div style=""font-family:Arial,sans-serif; max-width:600px; margin:auto; background:#0a0a1a; color:#ffffff; border-radius:12px; overflow:hidden;"">
            <div style=""background:linear-gradient(135deg,#1a1a3e,#0d6efd); padding:40px; text-align:center;"">
                <h1 style=""margin:0; font-size:28px;"">✈️ AeroFly</h1>
                <p style=""margin:8px 0 0; opacity:.8;"">Flight Reservation System</p>
            </div>
            <div style=""padding:40px;"">
                <h2 style=""color:#4da6ff; margin-top:0;"">Hello, {userName}! 👋</h2>
                <p style=""color:#ccc; line-height:1.7;"">Thank you for registering. Please click the button below to activate your account:</p>
                <div style=""text-align:center; margin:35px 0;"">
                    <a href=""{confirmLink}"" style=""background:linear-gradient(135deg,#0d6efd,#0dcaf0); color:#fff; padding:15px 40px; border-radius:50px; text-decoration:none; font-size:16px; font-weight:bold;"">
                        ✅ Confirm Email
                    </a>
                </div>
                <p style=""color:#888; font-size:13px;"">⏰ This link is valid for <strong style=""color:#ffc107;"">24 hours</strong> only.</p>
            </div>
            <div style=""background:#111; padding:20px; text-align:center; color:#555; font-size:12px;"">
                &copy; 2025 AeroFly. All rights reserved.
            </div>
        </div>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendOtpEmailAsync(string toEmail, string userName, string otpCode)
    {
        var subject = "🔐 Your OTP Verification Code - AeroFly";
        var body = $@"
        <div style=""font-family:Arial,sans-serif; max-width:600px; margin:auto; background:#0a0a1a; color:#ffffff; border-radius:12px; overflow:hidden;"">
            <div style=""background:linear-gradient(135deg,#1a1a3e,#0d6efd); padding:40px; text-align:center;"">
                <h1 style=""margin:0; font-size:28px;"">✈️ AeroFly</h1>
            </div>
            <div style=""padding:40px;"">
                <h2 style=""color:#4da6ff; margin-top:0;"">Hello, {userName}! 🔐</h2>
                <p style=""color:#ccc; line-height:1.7;"">Your verification code is:</p>
                <div style=""background:#111827; border:2px solid #0d6efd; border-radius:12px; padding:25px; text-align:center; margin:25px 0;"">
                    <span style=""font-size:42px; font-weight:bold; letter-spacing:8px; color:#4da6ff; font-family:monospace;"">{otpCode}</span>
                </div>
                <p style=""color:#888; font-size:13px;"">⏰ This code is valid for <strong style=""color:#ffc107;"">10 minutes</strong> only.</p>
            </div>
            <div style=""background:#111; padding:20px; text-align:center; color:#555; font-size:12px;"">
                &copy; 2025 AeroFly. All rights reserved.
            </div>
        </div>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendResetPasswordEmailAsync(string toEmail, string userName, string resetLink)
    {
        var subject = "🔑 Reset Your Password - AeroFly";
        var body = $@"
        <div style=""font-family:Arial,sans-serif; max-width:600px; margin:auto; background:#0a0a1a; color:#ffffff; border-radius:12px; overflow:hidden;"">
            <div style=""background:linear-gradient(135deg,#1a1a3e,#dc3545); padding:40px; text-align:center;"">
                <h1 style=""margin:0; font-size:28px;"">✈️ AeroFly</h1>
            </div>
            <div style=""padding:40px;"">
                <h2 style=""color:#ff6b6b; margin-top:0;"">Reset Your Password 🔑</h2>
                <p style=""color:#ccc; line-height:1.7;"">Hello {userName}, we received a request to reset your password. Click the button below to proceed:</p>
                <div style=""text-align:center; margin:35px 0;"">
                    <a href=""{resetLink}"" style=""background:linear-gradient(135deg,#dc3545,#fd7e14); color:#fff; padding:15px 40px; border-radius:50px; text-decoration:none; font-size:16px; font-weight:bold;"">
                        🔑 Reset Password
                    </a>
                </div>
                <p style=""color:#888; font-size:13px;"">⏰ This link is valid for <strong style=""color:#ffc107;"">1 hour</strong> only.</p>
            </div>
            <div style=""background:#111; padding:20px; text-align:center; color:#555; font-size:12px;"">
                &copy; 2025 AeroFly. All rights reserved.
            </div>
        </div>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendBookingConfirmationEmailAsync(string toEmail, string userName, BookingConfirmationVM booking)
    {
        var subject = $"✈️ Booking Confirmed - {booking.FlightNumber} - AeroFly";

        var departureDate = booking.DepartureTime.ToString("dd MMM yyyy");
        var departureTime = booking.DepartureTime.ToString("HH:mm");
        var arrivalTime = booking.ArrivalTime.ToString("HH:mm");

        var passengerRows = string.Join("\n", booking.Passengers.Select(p =>
            $@"<tr>
                <td style=""padding:8px 12px; border-bottom:1px solid #e9ecef;"">{p.FullName}</td>
                <td style=""padding:8px 12px; border-bottom:1px solid #e9ecef;"">{p.PassportNumber}</td>
                <td style=""padding:8px 12px; border-bottom:1px solid #e9ecef;"">{p.Age}</td>
               </tr>"));

        var body = $@"
<!DOCTYPE html>
<html>
<body style=""font-family:Arial,sans-serif; background:#f4f4f4; margin:0; padding:20px;"">

  <div style=""max-width:600px; margin:0 auto; background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.1);"">

    <!-- Header -->
    <div style=""background:linear-gradient(135deg,#0a1628,#1a3a6b); padding:30px; text-align:center;"">
      <h1 style=""color:#fff; margin:0; font-size:28px;"">✈️ AeroFly</h1>
      <p style=""color:rgba(255,255,255,0.8); margin:5px 0 0;"">Booking Confirmation</p>
    </div>

    <!-- Body -->
    <div style=""padding:30px;"">
      <h2 style=""color:#1a3a6b; margin-top:0;"">Hello, {userName}! 👋</h2>
      <p style=""color:#555;"">Your booking has been confirmed. Here are your flight details:</p>

      <!-- Booking ID -->
      <div style=""background:#0a1628; color:#fff; padding:15px; border-radius:8px; text-align:center; margin:15px 0;"">
        <div style=""font-size:13px; opacity:0.8;"">Booking Reference</div>
        <div style=""font-size:28px; font-weight:700; letter-spacing:4px; color:#4da6ff; font-family:monospace;"">#{booking.BookingId}</div>
      </div>

      <!-- Route -->
      <div style=""display:flex; justify-content:center; align-items:center; padding:20px 0; text-align:center;"">
        <div>
          <div style=""font-size:22px; font-weight:700; color:#1a1a2e;"">{departureTime}</div>
          <div style=""font-size:14px; font-weight:600;"">{booking.Route.Split('→')[0].Trim()}</div>
        </div>
        <div style=""padding:0 20px; color:#0d6efd; font-size:22px;"">✈️</div>
        <div>
          <div style=""font-size:22px; font-weight:700; color:#1a1a2e;"">{arrivalTime}</div>
          <div style=""font-size:14px; font-weight:600;"">{(booking.Route.Contains('→') ? booking.Route.Split('→')[1].Trim() : "")}</div>
        </div>
      </div>
      <div style=""text-align:center; margin:-10px 0 15px;"">
        <span style=""background:#e9ecef; padding:4px 16px; border-radius:20px; font-size:12px; color:#6c757d;"">
          Flight: {booking.FlightNumber} | {departureDate}
        </span>
      </div>

      <!-- Details Table -->
      <div style=""background:#f8f9fa; border-radius:8px; padding:20px; margin:15px 0;"">
        <table style=""width:100%; border-collapse:collapse;"">
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600; width:40%;"">Flight</td>
            <td style=""padding:8px 0; color:#1a1a2e; font-weight:500;"">{booking.FlightNumber}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Route</td>
            <td style=""padding:8px 0; color:#1a1a2e; font-weight:500;"">{booking.Route}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Departure</td>
            <td style=""padding:8px 0; color:#1a1a2e; font-weight:500;"">{booking.DepartureTime:dd MMM yyyy HH:mm}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Arrival</td>
            <td style=""padding:8px 0; color:#1a1a2e; font-weight:500;"">{booking.ArrivalTime:dd MMM yyyy HH:mm}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Seat Class</td>
            <td style=""padding:8px 0; color:#1a1a2e; font-weight:500;"">{booking.SeatClass}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Passengers</td>
            <td style=""padding:8px 0; color:#1a1a2e; font-weight:500;"">{booking.PassengerCount}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Total Price</td>
            <td style=""padding:8px 0; font-size:18px; font-weight:700; color:#0d6efd;"">${booking.TotalPrice:N2}</td>
          </tr>
          <tr>
            <td style=""padding:8px 0; color:#6c757d; font-weight:600;"">Status</td>
            <td style=""padding:8px 0; color:#28a745; font-weight:600;"">{booking.Status}</td>
          </tr>
        </table>
      </div>

      <!-- Passengers Table -->
      <h4 style=""margin-top:20px; color:#1a3a6b;"">👤 Passengers</h4>
      <table style=""width:100%; border-collapse:collapse; font-size:14px;"">
        <thead>
          <tr style=""background:#0a1628; color:#fff;"">
            <th style=""padding:10px 12px; text-align:left;"">Full Name</th>
            <th style=""padding:10px 12px; text-align:left;"">Passport</th>
            <th style=""padding:10px 12px; text-align:left;"">Age</th>
          </tr>
        </thead>
        <tbody>
          {passengerRows}
        </tbody>
      </table>

      <!-- CTA Button -->
      <div style=""text-align:center; margin:30px 0 20px;"">
        <a href=""https://localhost:7234/User/Booking/Confirmation?bookingId={booking.BookingId}""
           style=""display:inline-block; padding:12px 30px; background:#0d6efd; color:#fff; text-decoration:none; border-radius:50px; font-weight:600;"">
          View Booking Details
        </a>
      </div>

      <p style=""color:#6c757d; font-size:13px; text-align:center;"">
        📱 Need help? Contact us at support@aerofly.com
      </p>
    </div>

    <!-- Footer -->
    <div style=""background:#f8f9fa; padding:20px; text-align:center; color:#6c757d; font-size:12px;"">
      <p style=""margin:0;"">&copy; 2025 AeroFly. All rights reserved.</p>
      <p style=""margin:4px 0 0;"">This is an automated email. Please do not reply.</p>
    </div>

  </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, body);
    }
}