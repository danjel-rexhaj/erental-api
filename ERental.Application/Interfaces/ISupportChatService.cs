namespace ERental.Application.Interfaces;

// Role is "user" or "model" (Gemini's name for the assistant side of the conversation).
public record SupportChatMessage(string Role, string Text);

public enum SupportChatStatus { Ok, NotConfigured, QuotaExceeded, Failed }

public record SupportChatResult(SupportChatStatus Status, string? Reply);

public interface ISupportChatService
{
    // False until Gemini:ApiKey is set -- the frontend hides the widget entirely in that case.
    bool IsConfigured { get; }

    Task<SupportChatResult> ReplyAsync(IReadOnlyList<SupportChatMessage> conversation, string? lang);
}
