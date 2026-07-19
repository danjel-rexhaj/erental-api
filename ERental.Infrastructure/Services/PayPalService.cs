using ERental.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ERental.Infrastructure.Services;

public class PayPalService : IPayPalService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public PayPalService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _http.BaseAddress = new Uri(config["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com");
        _config = config;
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        var clientId = _config["PayPal:ClientId"];
        var secret = _config["PayPal:Secret"];
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString();
    }

    private static PayPalCaptureResult ParseCapture(JsonElement captureJson)
    {
        var status = captureJson.GetProperty("status").GetString();
        var id = captureJson.GetProperty("id").GetString();
        var amountEl = captureJson.GetProperty("amount");
        var value = decimal.Parse(amountEl.GetProperty("value").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var currency = amountEl.GetProperty("currency_code").GetString();
        return new PayPalCaptureResult(status == "COMPLETED", id, value, currency, status, null);
    }

    public async Task<PayPalOrderResult> CreateOrderAsync(decimal amount, string currency = "EUR")
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return new PayPalOrderResult(false, null, "Autentikimi me PayPal deshtoi.");

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new { amount = new { currency_code = currency, value = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) } }
            }
        });

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        if (!res.IsSuccessStatusCode)
        {
            var msg = body.TryGetProperty("message", out var m) ? m.GetString() : "Krijimi i porosise ne PayPal deshtoi.";
            return new PayPalOrderResult(false, null, msg);
        }

        return new PayPalOrderResult(true, body.GetProperty("id").GetString(), null);
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string orderId)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return new PayPalCaptureResult(false, null, null, null, null, "Autentikimi me PayPal deshtoi.");

        // PayPal's card-guest-checkout approval can land a beat after the browser's onApprove fires
        // (ORDER_NOT_APPROVED), so retry briefly before treating it as a genuine failure.
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{orderId}/capture");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent("", Encoding.UTF8, "application/json");

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (res.IsSuccessStatusCode)
            {
                var capture = body.GetProperty("purchase_units")[0].GetProperty("payments").GetProperty("captures")[0];
                return ParseCapture(capture);
            }

            var issue = body.TryGetProperty("details", out var details) && details.GetArrayLength() > 0
                ? details[0].GetProperty("issue").GetString()
                : null;

            if (issue == "ORDER_NOT_APPROVED" && attempt < 3)
            {
                await Task.Delay(1200 * attempt);
                continue;
            }

            var msg = body.TryGetProperty("message", out var m) ? m.GetString() : "Pagesa nuk u pranua nga PayPal.";
            return new PayPalCaptureResult(false, null, null, null, null, msg);
        }

        return new PayPalCaptureResult(false, null, null, null, null, "Pagesa nuk u pranua nga PayPal.");
    }

    public async Task<PayPalCaptureResult> GetCaptureAsync(string captureId)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return new PayPalCaptureResult(false, null, null, null, null, "Autentikimi me PayPal deshtoi.");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/v2/payments/captures/{captureId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            return new PayPalCaptureResult(false, null, null, null, null, "Nuk u gjet pagesa ne PayPal.");

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return ParseCapture(body);
    }

    public async Task<bool> RefundCaptureAsync(string captureId, decimal amount, string currency = "EUR")
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return false;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/payments/captures/{captureId}/refund");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new
        {
            amount = new { value = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), currency_code = currency }
        });

        var res = await _http.SendAsync(req);
        return res.IsSuccessStatusCode;
    }
}
