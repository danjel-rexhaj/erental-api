using ERental.Application.Interfaces;
using ERental.Infrastructure.Entities;
using ERental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ERental.Controllers;

public record RegisterDto(string Emri, string Mbiemri, string Email, string Password, string? Telefoni, bool HasWhatsapp, string? Kombesia);
public record LoginDto(string Email, string Password);
public record AuthResponseDto(string Token, string Email, string Emri, string Mbiemri, string? Telefoni, bool HasWhatsapp, bool EmailVerified, bool HasCompany);
public record VerifyEmailDto(string Email, string Code);
public record ResendDto(string Email);
public record ChangePasswordConfirmDto(string Code, string NewPassword);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Code, string NewPassword);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ERentalDbContext _context;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public AuthController(ERentalDbContext context, IConfiguration config, IEmailService emailService)
    {
        _context = context;
        _config = config;
        _emailService = emailService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email-i eshte i regjistruar tashme.");

        var code = new Random().Next(100000, 999999).ToString();
        var expiry = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(15), DateTimeKind.Unspecified);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var existing = await _context.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == dto.Email);
        if (existing != null)
        {
            existing.Emri = dto.Emri;
            existing.Mbiemri = dto.Mbiemri;
            existing.PasswordHash = passwordHash;
            existing.Telefoni = dto.Telefoni;
            existing.HasWhatsapp = dto.HasWhatsapp;
            existing.Kombesia = dto.Kombesia;
            existing.Code = code;
            existing.DataSkadimit = expiry;
        }
        else
        {
            _context.PendingRegistrations.Add(new PendingRegistration
            {
                Email = dto.Email,
                Emri = dto.Emri,
                Mbiemri = dto.Mbiemri,
                PasswordHash = passwordHash,
                Telefoni = dto.Telefoni,
                HasWhatsapp = dto.HasWhatsapp,
                Kombesia = dto.Kombesia,
                Code = code,
                DataSkadimit = expiry
            });
        }

        try
        {
            await _context.SaveChangesAsync();
            await _emailService.SendVerificationCodeAsync(dto.Email, dto.Emri, code);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return StatusCode(500, "Dergimi i email-it deshtoi. Provo perseri.");
        }

        return Ok(new { message = "Kod u dergua ne email.", email = dto.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            try
            {
                _context.LoginLogs.Add(new LoginLog { Email = dto.Email, UserId = user?.UserId, IpAddress = ip, Sukses = false });
                await _context.SaveChangesAsync();
            }
            catch { }

            return Unauthorized("Email ose fjalekalim gabim.");
        }

        try
        {
            _context.LoginLogs.Add(new LoginLog { Email = dto.Email, UserId = user.UserId, IpAddress = ip, Sukses = true });
            await _context.SaveChangesAsync();
        }
        catch { }

        bool hasCompany = await _context.Companies.AnyAsync(c => c.OwnerUserId == user.UserId);

        var token = GenerateToken(user.Email, user.UserId);
        return Ok(new AuthResponseDto(token, user.Email, user.Emri, user.Mbiemri, user.Telefoni, user.HasWhatsapp ?? false, user.EmailVerified ?? false, hasCompany));
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto dto)
    {
        var pending = await _context.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email == dto.Email && p.Code == dto.Code);

        if (pending == null) return BadRequest("Kod i pasakte.");
        if (pending.DataSkadimit < DateTime.UtcNow) return BadRequest("Kodi ka skaduar. Kerko nje te ri.");

        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email-i eshte i regjistruar tashme.");

        var user = new User
        {
            Emri = pending.Emri,
            Mbiemri = pending.Mbiemri,
            Email = pending.Email,
            PasswordHash = pending.PasswordHash,
            Telefoni = pending.Telefoni,
            HasWhatsapp = pending.HasWhatsapp,
            Kombesia = pending.Kombesia,
            EmailVerified = true
        };

        _context.Users.Add(user);
        _context.PendingRegistrations.Remove(pending);
        await _context.SaveChangesAsync();

        try { await _emailService.SendWelcomeAsync(user.Email, user.Emri); } catch (Exception ex) { Console.WriteLine($"Welcome email error: {ex.Message}"); }

        var token = GenerateToken(user.Email, user.UserId);
        return Ok(new AuthResponseDto(token, user.Email, user.Emri, user.Mbiemri, user.Telefoni, user.HasWhatsapp ?? false, true, false));
    }

    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode(ResendDto dto)
    {
        var pending = await _context.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == dto.Email);
        if (pending == null) return NotFound("Nuk u gjet regjistrim ne pritje per kete email.");

        pending.Code = new Random().Next(100000, 999999).ToString();
        pending.DataSkadimit = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(15), DateTimeKind.Unspecified);

        try
        {
            await _context.SaveChangesAsync();
            await _emailService.SendVerificationCodeAsync(pending.Email, pending.Emri, pending.Code);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return StatusCode(500, "Dergimi i email-it deshtoi.");
        }

        return Ok(new { message = "Kod i ri u dergua." });
    }

    [HttpPost("change-password/request")]
    [Authorize]
    public async Task<IActionResult> RequestPasswordChange()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var code = new Random().Next(100000, 999999).ToString();
        _context.EmailVerifications.Add(new EmailVerification
        {
            UserId = userId,
            Token = code,
            DataSkadimit = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(15), DateTimeKind.Unspecified),
            Perdorur = false
        });

        try
        {
            await _context.SaveChangesAsync();
            await _emailService.SendPasswordCodeAsync(user.Email, user.Emri, code);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return StatusCode(500, "Dergimi i email-it deshtoi.");
        }

        return Ok(new { message = "Kodi u dergua ne email." });
    }

    [HttpPost("change-password/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmPasswordChange(ChangePasswordConfirmDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var verification = await _context.EmailVerifications
            .Where(v => v.UserId == userId && v.Token == dto.Code && v.Perdorur != true && v.DataSkadimit > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
            .OrderByDescending(v => v.DataKrijimit)
            .FirstOrDefaultAsync();
        if (verification == null) return BadRequest("Kod i pasakte ose i skaduar.");

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        verification.Perdorur = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Fjalekalimi u ndryshua." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user != null)
        {
            var code = new Random().Next(100000, 999999).ToString();
            _context.EmailVerifications.Add(new EmailVerification
            {
                UserId = user.UserId,
                Token = code,
                DataSkadimit = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(15), DateTimeKind.Unspecified),
                Perdorur = false
            });

            try
            {
                await _context.SaveChangesAsync();
                await _emailService.SendPasswordCodeAsync(user.Email, user.Emri, code);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        return Ok(new { message = "Nese email-i ekziston, u dergua nje kod." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null) return BadRequest("Kod i pasakte ose i skaduar.");

        var verification = await _context.EmailVerifications
            .Where(v => v.UserId == user.UserId && v.Token == dto.Code && v.Perdorur != true && v.DataSkadimit > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified))
            .OrderByDescending(v => v.DataKrijimit)
            .FirstOrDefaultAsync();
        if (verification == null) return BadRequest("Kod i pasakte ose i skaduar.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        verification.Perdorur = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Fjalekalimi u ndryshua." });
    }

    private string GenerateToken(string email, int userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiryMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}