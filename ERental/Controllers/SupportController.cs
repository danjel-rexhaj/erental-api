using ERental.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace ERental.Controllers;

public record SupportChatTurnDto(string Role, string Text);
public record SupportChatDto(List<SupportChatTurnDto> Messages, string? Lang);

[ApiController]
[Route("api/[controller]")]
public class SupportController : ControllerBase
{
    // Public and unauthenticated (visitors ask before signing up), so these caps are the only thing
    // between a script and the free Gemini quota. In-memory is enough for the single Render instance.
    private const int MaxPerIp = 20;
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(10);
    private const int MaxPerDay = 800;
    private const int MaxMessageLength = 500;
    private const int MaxTurns = 12;

    private static readonly ConcurrentDictionary<string, List<DateTime>> RecentByIp = new();
    private static readonly object DayLock = new();
    private static DateOnly _day;
    private static int _dayCount;

    private readonly ISupportChatService _chat;
    public SupportController(ISupportChatService chat) => _chat = chat;

    [HttpGet("status")]
    public IActionResult Status() => Ok(new { enabled = _chat.IsConfigured });

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(SupportChatDto dto)
    {
        if (!_chat.IsConfigured) return StatusCode(503, new { code = "unavailable" });

        var turns = (dto.Messages ?? new())
            .Where(m => !string.IsNullOrWhiteSpace(m.Text) && (m.Role == "user" || m.Role == "model"))
            .TakeLast(MaxTurns)
            .Select(m => new SupportChatMessage(m.Role, m.Text.Trim().Length > MaxMessageLength ? m.Text.Trim()[..MaxMessageLength] : m.Text.Trim()))
            .SkipWhile(m => m.Role != "user")
            .ToList();
        if (turns.Count == 0 || turns[^1].Role != "user") return BadRequest(new { code = "empty" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!TryConsume(ip)) return StatusCode(429, new { code = "limit" });

        var result = await _chat.ReplyAsync(turns, dto.Lang);
        return result.Status switch
        {
            SupportChatStatus.Ok => Ok(new { reply = result.Reply }),
            SupportChatStatus.QuotaExceeded => StatusCode(429, new { code = "limit" }),
            _ => StatusCode(502, new { code = "failed" })
        };
    }

    private static bool TryConsume(string ip)
    {
        var now = DateTime.UtcNow;

        lock (DayLock)
        {
            var today = DateOnly.FromDateTime(now);
            if (today != _day) { _day = today; _dayCount = 0; RecentByIp.Clear(); }
            if (_dayCount >= MaxPerDay) return false;

            var recent = RecentByIp.GetOrAdd(ip, _ => new List<DateTime>());
            recent.RemoveAll(t => now - t > IpWindow);
            if (recent.Count >= MaxPerIp) return false;

            recent.Add(now);
            _dayCount++;
            return true;
        }
    }
}
