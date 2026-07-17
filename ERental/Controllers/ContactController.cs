using ERental.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ERental.Controllers;

public record ContactMessageDto(string Emri, string Email, string Subjekti, string Mesazhi);

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IEmailService _emailService;
    public ContactController(IEmailService emailService) => _emailService = emailService;

    [HttpPost]
    public async Task<IActionResult> Send(ContactMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Mesazhi))
            return BadRequest("Email-i dhe mesazhi jane te detyrueshem.");

        try
        {
            await _emailService.SendContactMessageAsync(dto.Emri, dto.Email, dto.Subjekti, dto.Mesazhi);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return StatusCode(500, "Dergimi i mesazhit deshtoi. Provo perseri.");
        }

        return Ok(new { message = "Mesazhi u dergua." });
    }
}
