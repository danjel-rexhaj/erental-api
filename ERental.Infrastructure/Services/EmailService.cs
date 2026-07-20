using ERental.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using QRCoder;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ERental.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config) => _config = config;

    private SendGridClient Client => new(_config["SendGrid:ApiKey"]);
    private EmailAddress From => new(_config["SendGrid:FromEmail"], _config["SendGrid:FromName"]);

    // SendGrid's client doesn't throw on a non-2xx response (rejected sender, bad template, rate
    // limit, etc.) — it just returns the Response object, so every failure was previously silent
    // (swallowed further by the callers' empty catch blocks). Logging here at least makes email
    // failures visible in the Render logs instead of just "the email never arrived, no idea why".
    private async Task SendAsync(SendGridMessage msg)
    {
        var response = await Client.SendEmailAsync(msg);
        var code = (int)response.StatusCode;
        if (code < 200 || code >= 300)
        {
            var body = await response.Body.ReadAsStringAsync();
            Console.WriteLine($"SendGrid email failed ({code}): {body}");
        }
    }

    private static readonly string[] Dite = { "Hënë", "Martë", "Mërkurë", "Enjte", "Premte", "Shtunë", "Diel" };
    private static readonly string[] Muaj = { "Janar", "Shkurt", "Mars", "Prill", "Maj", "Qershor", "Korrik", "Gusht", "Shtator", "Tetor", "Nëntor", "Dhjetor" };

    private static string FormatDate(string raw)
    {
        if (!DateOnly.TryParse(raw, out var d)) return raw;
        int dow = ((int)d.DayOfWeek + 6) % 7;
        return $"{Dite[dow]}, {d.Day} {Muaj[d.Month - 1]} {d.Year}";
    }

    private static string Confirmim(int bookingId) => $"ER-{bookingId:D6}";

    private static string MapsLink(string address, string city) =>
        $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString($"{address}, {city}, Shqipëri")}";

    // ---- layout shell ----------------------------------------------------

    private string Wrap(string bodyHtml, string preheader = "")
    {
        var preheaderHtml = string.IsNullOrEmpty(preheader) ? "" : $@"
          <div style='display:none; max-height:0; overflow:hidden; mso-hide:all;'>{preheader}</div>";

        return $@"
        {preheaderHtml}
        <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 560px; margin: 0 auto; background:#ffffff; color:#222222;'>

          <div style='height:5px; background:linear-gradient(90deg,#2dd4bf,#0f766e);'></div>

          <div style='padding:24px 40px; border-bottom:1px solid #ebebeb;'>
            <table role='presentation' cellpadding='0' cellspacing='0'>
              <tr>
                <td style='width:32px; vertical-align:middle;'>
                  <div style='width:30px; height:30px; border-radius:50%; background:#0f766e; color:#ffffff; text-align:center; line-height:30px; font-weight:800; font-size:13px;'>ER</div>
                </td>
                <td style='padding-left:10px; vertical-align:middle;'>
                  <span style='font-size:18px; font-weight:800; color:#111111; letter-spacing:-0.4px;'>ERental</span>
                </td>
              </tr>
            </table>
          </div>

          <div style='padding:40px;'>
              {bodyHtml}
          </div>

          <div style='padding:28px 40px 32px 40px; border-top:1px solid #ebebeb;'>
            <p style='color:#111111; font-size:13px; font-weight:700; margin:0 0 4px 0;'>ERental Albania</p>
            <p style='color:#767676; font-size:12px; line-height:1.6; margin:0 0 12px 0;'>
                Platforma që lidh biznese të verifikuara të makinave me qera me klientët në të gjithë Shqipërinë.
            </p>
            <p style='color:#767676; font-size:12px; line-height:1.6; margin:0;'>
                <a href='mailto:info@erental.store' style='color:#0f766e; text-decoration:underline;'>info@erental.store</a>
                &nbsp;·&nbsp; Ky email u dërgua sepse ke një veprim aktiv në llogarinë tënde ERental.
            </p>
          </div>

        </div>";
    }

    // ---- shared building blocks -------------------------------------------

    private string SectionLabel(string text) => $@"
        <p style='color:#717171; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.6px; margin:0 0 4px 0;'>{text}</p>";

    private string DetailsTable(params (string Label, string Value)[] rows)
    {
        var rowsHtml = string.Concat(rows.Select(r => $@"
          <tr>
            <td style='padding:14px 0; border-top:1px solid #ebebeb; color:#717171; font-size:14px;'>{r.Label}</td>
            <td style='padding:14px 0; border-top:1px solid #ebebeb; color:#111111; font-size:14px; font-weight:600; text-align:right;'>{r.Value}</td>
          </tr>"));
        return $@"<table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin:24px 0;'>{rowsHtml}</table>";
    }

    private string PersonRow(string name, string subtitle) => $@"
        <table role='presentation' cellpadding='0' cellspacing='0' style='margin-bottom:8px;'>
          <tr>
            <td style='width:44px; vertical-align:top;'>
              <div style='width:40px;height:40px;border-radius:50%;background:#f7f7f7;border:1px solid #ebebeb;color:#111111;text-align:center;line-height:40px;font-weight:700;font-size:16px;'>
                {(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper())}
              </div>
            </td>
            <td style='padding-left:14px; vertical-align:middle;'>
              <div style='font-size:15px;font-weight:600;color:#111111;'>{name}</div>
              <div style='font-size:13px;color:#717171;'>{subtitle}</div>
            </td>
          </tr>
        </table>";

    private string CodeBox(string code) => $@"
        <div style='text-align:center; padding:20px 0; margin:28px 0;'>
            <span style='font-size:36px; font-weight:800; letter-spacing:8px; color:#111111;'>{code}</span>
        </div>
        <p style='color:#717171; font-size:13px; text-align:center; margin:0;'>Ky kod skadon pas 15 minutash. Mos e ndaj me askënd.</p>";

    // Airbnb-style reservation card: photo, pickup/return dates, location, confirmation code, price, host contact.
    private string TripCard(string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, int bookingId, decimal? total = null, string? carPhotoUrl = null, string? address = null, string? city = null, string? phone = null)
    {
        var image = string.IsNullOrEmpty(carPhotoUrl) ? "" : $@"
            <img src='{carPhotoUrl}' alt='{makina}' style='width:100%; height:200px; object-fit:cover; display:block;' />";

        var addressRow = string.IsNullOrEmpty(address) ? "" : $@"
            <div style='margin-top:20px; padding-top:20px; border-top:1px solid #ebebeb;'>
              {SectionLabel("Vendndodhja")}
              <p style='font-size:14px; color:#111111; margin:0 0 6px 0;'>{address}{(string.IsNullOrEmpty(city) ? "" : $", {city}")}</p>
              <a href='{MapsLink(address, city ?? "")}' style='font-size:13px; font-weight:600; color:#0f766e; text-decoration:underline;'>Merr udhëzime →</a>
            </div>";

        var priceRow = total.HasValue ? $@"
            <tr>
              <td style='padding:14px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Çmimi total</td>
              <td style='padding:14px 0; border-top:1px solid #ebebeb; color:#111111; font-size:13px; font-weight:700; text-align:right;'>{total.Value}€</td>
            </tr>" : "";

        var contactRow = string.IsNullOrEmpty(phone) ? "" : $@"
            <div style='margin-top:20px; padding-top:20px; border-top:1px solid #ebebeb;'>
              {PersonRow(bizniEmri, "Biznesi që ofron këtë makinë")}
              <a href='tel:{phone}' style='display:inline-block; margin-top:10px; font-size:13px; font-weight:700; color:#ffffff; background:#0f766e; border-radius:8px; padding:10px 16px; text-decoration:none;'>Telefono {phone}</a>
            </div>";

        return $@"
        <div style='border:1px solid #ebebeb; border-radius:16px; overflow:hidden; margin:8px 0 28px 0;'>
            {image}
            <div style='padding:24px;'>
                {SectionLabel("Makinë me qera")}
                <h2 style='font-size:19px; font-weight:800; color:#111111; margin:0 0 2px 0;'>{makina}</h2>
                <p style='font-size:13px; color:#717171; margin:0 0 20px 0;'>Nga {bizniEmri}</p>

                <table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
                  <tr>
                    <td width='50%' style='padding-right:14px; border-right:1px solid #ebebeb; vertical-align:top;'>
                      {SectionLabel("Marrja")}
                      <p style='font-size:15px; font-weight:700; color:#111111; margin:0;'>{FormatDate(dataFillimit)}</p>
                    </td>
                    <td width='50%' style='padding-left:14px; vertical-align:top;'>
                      {SectionLabel("Dorëzimi")}
                      <p style='font-size:15px; font-weight:700; color:#111111; margin:0;'>{FormatDate(dataPerfundimit)}</p>
                    </td>
                  </tr>
                </table>

                {addressRow}

                <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin-top:8px;'>
                  <tr>
                    <td style='padding:14px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Numri i konfirmimit</td>
                    <td style='padding:14px 0; border-top:1px solid #ebebeb; color:#111111; font-size:13px; font-weight:700; text-align:right; letter-spacing:0.5px;'>{Confirmim(bookingId)}</td>
                  </tr>
                  {priceRow}
                </table>

                {contactRow}
            </div>
        </div>";
    }

    private static string QrDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    // ---- emails -------------------------------------------------------

    public async Task SendVerificationCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Verifiko email-in tënd</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje {emri}, përdor kodin më poshtë për të verifikuar email-in dhe për të aktivizuar llogarinë ERental.
            </p>

            {CodeBox(code)}";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kodi yt i verifikimit — ERental", "", Wrap(body, "Përdor këtë kod për të verifikuar email-in tënd"));
        await SendAsync(msg);
    }


    public async Task SendBookingPendingToClientAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? carPhotoUrl = null)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:24px; font-weight:800; margin:0 0 6px 0;'>Kërkesa u dërgua</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Përshëndetje <strong>{emri}</strong>, rezervimi yt është dërguar dhe pret miratimin e biznesit.
            </p>

            {TripCard(makina, "", dataFillimit, dataPerfundimit, bookingId, total, carPhotoUrl)}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Do të njoftohesh me email dhe në platformë sapo biznesi ta shqyrtojë kërkesën.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi yt është në pritje — ERental", "", Wrap(body, $"Kërkesa jote për {makina} pret miratim"));
        await SendAsync(msg);
    }


    public async Task SendBookingRequestToBusinessAsync(string toEmail, string bizniEmri, string makina, string klientiEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 20px 0;'>Kërkesë e re rezervimi</h1>

            {PersonRow(klientiEmri, $"kërkoi të rezervojë {makina}")}

            {DetailsTable(("Makina", makina), ("Marrja", FormatDate(dataFillimit)), ("Dorëzimi", FormatDate(dataPerfundimit)))}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Përshëndetje {bizniEmri}, hyr në panelin e biznesit për ta miratuar ose refuzuar kërkesën.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kërkesë e re rezervimi — ERental", "", Wrap(body, $"{klientiEmri} kërkoi të rezervojë {makina}"));
        await SendAsync(msg);
    }


    public async Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? companyAddress, string? companyCity, string? companyPhone, string? carPhotoUrl, string? contractUrl = null)
    {
        var contractButton = string.IsNullOrEmpty(contractUrl) ? "" : $@"
            <div style='text-align:center; margin:0 0 24px 0;'>
                <a href='{contractUrl}' style='display:inline-block; background:#111111; color:#ffffff; font-size:14px; font-weight:700; text-decoration:none; padding:12px 22px; border-radius:8px;'>
                    Merr kontratën e qerasë
                </a>
            </div>";

        var body = $@"
            <h1 style='color:#111111; font-size:24px; font-weight:800; margin:0 0 6px 0;'>Rezervimi u konfirmua ✓</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Përshëndetje <strong>{emri}</strong>, <strong>{bizniEmri}</strong> e konfirmoi rezervimin tënd. Je gati për udhëtimin.
            </p>

            {TripCard(makina, bizniEmri, dataFillimit, dataPerfundimit, bookingId, total, carPhotoUrl, companyAddress, companyCity, companyPhone)}

            {contractButton}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Shko në adresën e biznesit në datën e caktuar. Nëse ndryshon plan, mund ta anulosh nga ""Rezervimet e mia"" në ERental.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi u konfirmua — ERental", "", Wrap(body, $"Gati për udhëtimin — {makina} nga {bizniEmri}"));
        await SendAsync(msg);
    }


    public async Task SendBookingCancelledAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, int bookingId, string? carPhotoUrl = null, string? arsyeja = null)
    {
        var reasonBlock = string.IsNullOrWhiteSpace(arsyeja) ? "" : $@"
            <div style='background:#f7f7f7; border-radius:12px; padding:16px; margin:0 0 24px 0;'>
                <p style='color:#717171; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.5px; margin:0 0 6px 0;'>Arsyeja</p>
                <p style='color:#222222; font-size:14px; line-height:1.6; margin:0;'>{arsyeja}</p>
            </div>";

        var body = $@"
            <h1 style='color:#111111; font-size:24px; font-weight:800; margin:0 0 6px 0;'>Rezervimi u anulua</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Përshëndetje <strong>{emri}</strong>, rezervimi yt për <strong>{makina}</strong> është anuluar.
            </p>

            {reasonBlock}

            {TripCard(makina, "", dataFillimit, dataPerfundimit, bookingId, null, carPhotoUrl)}";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi u anulua — ERental", "", Wrap(body, $"Rezervimi për {makina} u anulua"));
        await SendAsync(msg);
    }


    public async Task SendPaymentReceiptAsync(string toEmail, string emri, string makina, string counterpartyName, decimal amountPaid, bool eshtePagesePlote, int bookingId, bool perBiznesin, decimal totalPrice, string dataFillimit, string dataPerfundimit)
    {
        var titulli = perBiznesin ? "Ke marre nje pagese te re" : "Pagesa u krye me sukses";
        var pershkrimi = perBiznesin
            ? $"Klienti <strong>{counterpartyName}</strong> pagoi {(eshtePagesePlote ? "shumen e plote" : "depoziten (1 dite)")} per <strong>{makina}</strong> me karte."
            : $"Pagesa jote per <strong>{makina}</strong> prane <strong>{counterpartyName}</strong> u krye me sukses me karte.";

        int dite = 1;
        if (DateOnly.TryParse(dataFillimit, out var df) && DateOnly.TryParse(dataPerfundimit, out var dp))
            dite = Math.Max(1, dp.DayNumber - df.DayNumber);
        var cmimiPerDite = Math.Round(totalPrice / dite, 2);
        var mbetetCash = totalPrice - amountPaid;

        var headerRow = $@"
            <tr>
                <td style='padding-bottom:8px; color:#a3a3a3; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.5px;'>Përshkrimi</td>
                <td style='padding-bottom:8px; color:#a3a3a3; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.5px; text-align:right;'>Sasia</td>
                <td style='padding-bottom:8px; color:#a3a3a3; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.5px; text-align:right;'>Shuma</td>
            </tr>";

        var itemRow = $@"
            <tr>
                <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#111111; font-size:13px;'>Qera — {makina}<br/><span style='color:#a3a3a3; font-size:11px;'>{FormatDate(dataFillimit)} → {FormatDate(dataPerfundimit)}</span></td>
                <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px; text-align:right; white-space:nowrap;'>{dite} × {cmimiPerDite}€</td>
                <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#111111; font-size:13px; font-weight:700; text-align:right; white-space:nowrap;'>{totalPrice}€</td>
            </tr>";

        var paidRow = $@"
            <tr>
                <td colspan='2' style='padding:12px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Paguar me kartë ({(eshtePagesePlote ? "pagesë e plotë" : "depozitë")})</td>
                <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#0f766e; font-size:14px; font-weight:800; text-align:right; white-space:nowrap;'>{amountPaid}€</td>
            </tr>";

        var cashRow = eshtePagesePlote ? "" : $@"
            <tr>
                <td colspan='2' style='padding:12px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Mbetet për t'u paguar cash</td>
                <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#b45309; font-size:14px; font-weight:800; text-align:right; white-space:nowrap;'>{mbetetCash}€</td>
            </tr>";

        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:800; margin:0 0 2px 0;'>Faturë</h1>
            <p style='color:#a3a3a3; font-size:12px; margin:0 0 24px 0;'>Nr. {Confirmim(bookingId)} · {DateTime.UtcNow:dd.MM.yyyy}</p>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Përshëndetje <strong>{emri}</strong>, {pershkrimi}
            </p>

            <div style='border:1px solid #ebebeb; border-radius:16px; padding:24px;'>
                <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%;'>
                    {headerRow}
                    {itemRow}
                    {paidRow}
                    {cashRow}
                </table>
            </div>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), $"{titulli} — ERental", "", Wrap(body, titulli));
        await SendAsync(msg);
    }


    public async Task SendReviewRequestAsync(string toEmail, string emri, string makina, string bizniEmri)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Si ishte qeraja?</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 14px 0;'>
                Përshëndetje <strong>{emri}</strong>, shpresojmë që qeraja e <strong>{makina}</strong> nga <strong>{bizniEmri}</strong> shkoi mirë.
            </p>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Na ndihmo dhe klientë të tjerë duke lënë një vlerësim — hyr te ""Rezervimet"" në ERental.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Le nje vleresim per qerane tende — ERental", "", Wrap(body, $"Si shkoi qeraja e {makina}?"));
        await SendAsync(msg);
    }


    public async Task SendPasswordCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Kodi për fjalëkalimin</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje <strong>{emri}</strong>, përdor kodin më poshtë për të vazhduar me ndryshimin e fjalëkalimit tënd.
            </p>

            {CodeBox(code)}

            <p style='color:#717171; font-size:13px; text-align:center; margin:8px 0 0 0;'>
                Nëse s'e ke kërkuar ti, shpërnfille këtë email.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kodi per fjalekalimin — ERental", "", Wrap(body, "Kodi yt për ndryshimin e fjalëkalimit"));
        await SendAsync(msg);
    }


    public async Task SendContactMessageAsync(string emriDerguesi, string emailDerguesi, string subjekti, string mesazhi)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>{subjekti}</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 16px 0;'>
                Mesazh i ri nga <strong>{emriDerguesi}</strong> (<a href='mailto:{emailDerguesi}' style='color:#111111;'>{emailDerguesi}</a>):
            </p>

            <div style='background:#f7f7f7; border-radius:12px; padding:20px; margin:0 0 16px 0; color:#222222; white-space:pre-line; font-size:14px; line-height:1.6;'>
                {mesazhi}
            </div>

            <p style='color:#717171; font-size:13px; margin:0;'>
                Përdor butonin Reply për t'iu përgjigjur direkt dërguesit.
            </p>";

        var msg = new SendGridMessage();
        msg.SetFrom(From);
        msg.AddTo(new EmailAddress("info@erental.store"));
        msg.SetReplyTo(new EmailAddress(emailDerguesi, emriDerguesi));
        msg.SetSubject($"{subjekti} — ERental");
        msg.AddContent("text/html", Wrap(body));

        await SendAsync(msg);
    }

    private string CheckBadge() => $@"
        <div style='width:56px; height:56px; border-radius:50%; background:#111111; text-align:center; line-height:56px; margin:0 0 20px 0;'>
          <span style='color:#ffffff; font-size:26px;'>&#10003;</span>
        </div>";

    public async Task SendAdminVerificationRequestAsync(string adminEmail, string companyName, int companyId)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:20px; font-weight:700; margin:0 0 16px 0;'>Kërkesë e re verifikimi</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Biznesi <strong>{companyName}</strong> (ID {companyId}) dërgoi certifikatën e NIPT-it dhe pret verifikim.
            </p>
            <a href='https://erental.store/#/biznesi?tab=admin' style='display:inline-block; background:#111111; color:#ffffff; font-size:14px; font-weight:700; text-decoration:none; padding:12px 20px; border-radius:8px;'>
                Shqyrto kërkesën
            </a>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(adminEmail), $"Kërkesë verifikimi — {companyName}", "", Wrap(body, $"{companyName} pret verifikim"));
        await SendAsync(msg);
    }

    public async Task SendCompanyVerifiedAsync(string toEmail, string emri, string companyName)
    {
        var body = $@"
            {CheckBadge()}
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Biznesi u verifikua</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje {emri}, <strong>{companyName}</strong> u shqyrtua dhe u verifikua nga ekipi i ERental. Makinat e tua tani shfaqen me shenjën ""I verifikuar"" dhe klientët mund t'i rezervojnë.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Biznesi yt u verifikua — ERental", "", Wrap(body, "Je gati të marrësh rezervime"));
        await SendAsync(msg);
    }

    public async Task SendWelcomeAsync(string toEmail, string emri)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Mirë se erdhe në ERental</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 16px 0;'>
                Përshëndetje {emri}, llogaria jote u aktivizua. ERental është platforma që lidh biznese të verifikuara të qerasë së makinave me klientë në të gjithë Shqipërinë — kërko, krahaso dhe rezervo, me pagesë të sigurt online.
            </p>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Kur të jesh gati, kërko makinën për datat e tua dhe rezervo direkt nga platforma.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Mirë se erdhe në ERental", "", Wrap(body, "Llogaria jote është gati"));
        await SendAsync(msg);
    }

    // Standalone HTML page (not an email) linked from the "Merr kontratën" button in the
    // booking-confirmed email — opened via GET /Bookings/{id}/contract/{token}, which isn't behind
    // [Authorize] since the recipient clicking an email link isn't necessarily logged in. Never
    // includes the license photos, only what's already visible elsewhere in the app.
    public string BuildContractHtmlPage(RentalContractDto dto)
    {
        var confirmim = Confirmim(dto.BookingId);
        var qrSrc = QrDataUri(dto.ContractUrl);

        var mbetetCash = dto.PaidOnline.HasValue ? dto.TotalPrice - dto.PaidOnline.Value : (decimal?)null;

        var carImage = string.IsNullOrEmpty(dto.CarPhotoUrl) ? "" : $@"
            <img src='{dto.CarPhotoUrl}' alt='{dto.CarMakeModel}' style='width:100%; height:180px; object-fit:cover; display:block;' />";

        var priceRows = new List<(string, string)>
        {
            ("Çmimi total", $"{dto.TotalPrice}€"),
            ("Mënyra e pagesës", dto.PaymentMethodLabel),
        };
        if (mbetetCash.HasValue && mbetetCash.Value > 0)
            priceRows.Add(("Mbetet për t'u paguar cash", $"{mbetetCash.Value}€"));

        var body = $@"
            <div style='text-align:center; margin-bottom:20px;'>
                <div style='width:48px; height:48px; border-radius:50%; background:#dcfce7; display:inline-flex; align-items:center; justify-content:center; margin-bottom:10px;'>
                    <span style='color:#166534; font-size:24px; font-weight:800;'>✓</span>
                </div>
                <p style='color:#166534; font-size:14px; font-weight:800; margin:0;'>Rezervim i verifikuar</p>
            </div>

            <div style='text-align:center; margin-bottom:24px;'>
                <p style='color:#0f766e; font-size:11px; font-weight:800; text-transform:uppercase; letter-spacing:1px; margin:0 0 6px 0;'>Kontratë Qeraje · Marketplace ERental</p>
                <h1 style='color:#111111; font-size:21px; font-weight:800; margin:0;'>Rezervimi {confirmim}</h1>
            </div>

            <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin-bottom:24px;'>
              <tr>
                <td width='50%' style='vertical-align:top; padding-right:12px;'>
                  {SectionLabel("Qeradhënësi")}
                  <p style='font-size:15px; font-weight:700; color:#111111; margin:0 0 2px 0;'>{dto.CompanyName}</p>
                  <p style='font-size:12px; color:#717171; margin:0;'>NIPT {dto.CompanyNipt}</p>
                  {(string.IsNullOrEmpty(dto.CompanyAddress) ? "" : $"<p style='font-size:12px; color:#717171; margin:0;'>{dto.CompanyAddress}{(string.IsNullOrEmpty(dto.CompanyCity) ? "" : $", {dto.CompanyCity}")}</p>")}
                  {(string.IsNullOrEmpty(dto.CompanyPhone) ? "" : $"<p style='font-size:12px; color:#717171; margin:0;'>{dto.CompanyPhone}</p>")}
                </td>
                <td width='50%' style='vertical-align:top; padding-left:12px; border-left:1px solid #ebebeb;'>
                  {SectionLabel("Qeramarrësi")}
                  <p style='font-size:15px; font-weight:700; color:#111111; margin:0 0 2px 0;'>{dto.ClientName}</p>
                  {(string.IsNullOrEmpty(dto.ClientPhone) ? "" : $"<p style='font-size:12px; color:#717171; margin:0;'>{dto.ClientPhone}</p>")}
                  {(string.IsNullOrEmpty(dto.ClientEmail) ? "" : $"<p style='font-size:12px; color:#717171; margin:0;'>{dto.ClientEmail}</p>")}
                </td>
              </tr>
            </table>

            <div style='border:1px solid #ebebeb; border-radius:16px; overflow:hidden; margin-bottom:8px;'>
                {carImage}
                <div style='padding:18px;'>
                    {SectionLabel("Objekti i qerasë")}
                    <p style='font-size:17px; font-weight:800; color:#111111; margin:0 0 2px 0;'>{dto.CarMakeModel} · {dto.CarYear}</p>
                    <p style='font-size:12px; color:#717171; margin:0;'>{dto.CarPlate}{(string.IsNullOrEmpty(dto.CarCategory) ? "" : $" · {dto.CarCategory}")}</p>

                    <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='margin-top:16px;'>
                      <tr>
                        <td width='50%' style='padding-right:14px; border-right:1px solid #ebebeb; vertical-align:top;'>
                          {SectionLabel("Marrja")}
                          <p style='font-size:14px; font-weight:700; color:#111111; margin:0;'>{FormatDate(dto.DataFillimit)}</p>
                        </td>
                        <td width='50%' style='padding-left:14px; vertical-align:top;'>
                          {SectionLabel("Dorëzimi")}
                          <p style='font-size:14px; font-weight:700; color:#111111; margin:0;'>{FormatDate(dto.DataPerfundimit)}</p>
                        </td>
                      </tr>
                    </table>

                    {DetailsTable(priceRows.ToArray())}
                </div>
            </div>

            <div style='text-align:center; margin:24px 0;'>
                <img src='{qrSrc}' width='130' height='130' alt='QR kodi i rezervimit' style='display:inline-block;' />
                <p style='font-size:11px; color:#717171; margin:8px 0 0 0;'>Skano për të verifikuar numrin e konfirmimit</p>
            </div>

            <p style='color:#a3a3a3; font-size:11px; line-height:1.6; margin:0; text-align:center;'>
                ERental është një marketplace që lidh biznese qeraje makinash me klientë. Ky dokument përmbledh marrëveshjen mes palëve të mësipërme — ERental nuk është palë në kontratë.
            </p>";

        return $@"<!doctype html>
<html lang='sq'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><title>Kontrata e qerasë · {confirmim}</title></head>
<body style='margin:0; background:#f7f7f7;'>
{Wrap(body, $"Kontrata për {dto.CarMakeModel}")}
</body></html>";
    }
}
