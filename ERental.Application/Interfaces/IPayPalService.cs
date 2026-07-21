namespace ERental.Application.Interfaces;

public record PayPalCaptureResult(bool Success, string? CaptureId, decimal? Amount, string? Currency, string? Status, string? Error, string? CardLast4 = null);
public record PayPalOrderResult(bool Success, string? OrderId, string? ApproveUrl, string? Error);

public interface IPayPalService
{
    Task<PayPalOrderResult> CreateOrderAsync(decimal amount, string currency = "EUR", string? returnUrl = null, string? cancelUrl = null);
    Task<PayPalCaptureResult> CaptureOrderAsync(string orderId);
    Task<PayPalCaptureResult> GetCaptureAsync(string captureId);
    Task<bool> RefundCaptureAsync(string captureId, decimal amount, string currency = "EUR");
}
