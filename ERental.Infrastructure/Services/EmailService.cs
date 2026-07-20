using ERental.Application.Interfaces;
using Microsoft.Extensions.Configuration;
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

    private static string ToWhatsappNumber(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "";
        if (digits.StartsWith("355")) return digits;
        if (digits.StartsWith("0")) return "355" + digits[1..];
        return digits;
    }

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

    // Minimal centered shell used only for one-time verification codes — deliberately plain
    // (no rich header/footer) so the code is the only thing that matters at a glance.
    private string WrapCode(string bodyHtml, string preheader = "")
    {
        var preheaderHtml = string.IsNullOrEmpty(preheader) ? "" : $@"
          <div style='display:none; max-height:0; overflow:hidden; mso-hide:all;'>{preheader}</div>";

        return $@"
        {preheaderHtml}
        <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 480px; margin: 0 auto; background:#ffffff; color:#222222; text-align:center; padding:48px 32px;'>

          <p style='font-size:20px; font-weight:800; letter-spacing:-0.4px; margin:0 0 40px 0;'>
            <span style='color:#d97706;'>E</span><span style='color:#111111;'>Rental</span>
          </p>

          {bodyHtml}

          <div style='height:1px; background:#ebebeb; margin:40px 0 24px 0;'></div>

          <p style='color:#a3a3a3; font-size:12px; line-height:1.6; margin:0;'>
              ERental Albania · <a href='mailto:info@erental.store' style='color:#a3a3a3; text-decoration:underline;'>info@erental.store</a>
          </p>
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
        <div style='background:#f0fdfa; border:1px solid #99f6e4; border-radius:12px; padding:28px; text-align:center; margin:28px 0;'>
            <span style='font-size:32px; font-weight:700; letter-spacing:8px; color:#0f766e;'>{code}</span>
        </div>
        <p style='color:#717171; font-size:13px; text-align:center; margin:0;'>Ky kod skadon pas 15 minutash.</p>";

    // Plain, borderless code display for the minimal WrapCode shell — the code itself carries an
    // autocomplete="one-time-code" hint on the matching frontend input, so keeping the number in a
    // predictable "code: NNNNNN" shape near the top of the email helps Mail/Safari suggest it on the keyboard.
    private string PlainCodeDisplay(string code) => $@"
        <p style='color:#484848; font-size:15px; margin:0 0 28px 0;'>Kodi yt i verifikimit:</p>
        <p style='font-size:40px; font-weight:800; letter-spacing:10px; color:#111111; margin:0 0 28px 4px;'>{code}</p>
        <p style='color:#a3a3a3; font-size:13px; line-height:1.6; margin:0;'>Kodi skadon pas 15 minutash. Mos e ndaj me askënd.</p>";

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

    // ---- emails -------------------------------------------------------

    public async Task SendVerificationCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <p style='color:#484848; font-size:15px; margin:0 0 4px 0;'>Përshëndetje {emri},</p>
            {PlainCodeDisplay(code)}";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kodi yt i verifikimit — ERental", "", WrapCode(body, "Përdor këtë kod për të verifikuar email-in tënd"));
        await Client.SendEmailAsync(msg);
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
        await Client.SendEmailAsync(msg);
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
        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? companyAddress = null, string? companyCity = null, string? companyPhone = null, string? carPhotoUrl = null)
    {
        var waNumber = ToWhatsappNumber(companyPhone);
        var docsBlock = waNumber == "" ? "" : $@"
            <div style='background:#f0fdf4; border:1px solid #bbf7d0; border-radius:12px; padding:16px; margin:0 0 24px 0;'>
                <p style='color:#166534; font-size:13px; font-weight:700; margin:0 0 8px 0;'>Para se te marrësh makinën</p>
                <p style='color:#166534; font-size:13px; line-height:1.6; margin:0 0 12px 0;'>
                    Dërgo në WhatsApp të <strong>{bizniEmri}</strong> një foto të <strong>patentës</strong> dhe <strong>kartës së identitetit (ID)</strong> të shoferit, që biznesi të verifikojë identitetin tënd para se të shkosh.
                </p>
                <a href='https://wa.me/{waNumber}' style='display:inline-block; background:#25D366; color:#ffffff; font-size:13px; font-weight:700; text-decoration:none; padding:10px 18px; border-radius:999px;'>
                    Dërgo në WhatsApp
                </a>
            </div>";

        var body = $@"
            <h1 style='color:#111111; font-size:24px; font-weight:800; margin:0 0 6px 0;'>Rezervimi u konfirmua ✓</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Përshëndetje <strong>{emri}</strong>, <strong>{bizniEmri}</strong> e konfirmoi rezervimin tënd. Je gati për udhëtimin.
            </p>

            {TripCard(makina, bizniEmri, dataFillimit, dataPerfundimit, bookingId, total, carPhotoUrl, companyAddress, companyCity, companyPhone)}

            {docsBlock}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Shko në adresën e biznesit në datën e caktuar — atje merr kontratën e nënshkruar dhe makinën. Mos u vono. Nëse ndryshon plan, mund ta anulosh nga ""Rezervimet e mia"" në ERental.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi u konfirmua — ERental", "", Wrap(body, $"Gati për udhëtimin — {makina} nga {bizniEmri}"));
        await Client.SendEmailAsync(msg);
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
        await Client.SendEmailAsync(msg);
    }


    public async Task SendPaymentReceiptAsync(string toEmail, string emri, string makina, string counterpartyName, decimal amountPaid, bool eshtePagesePlote, int bookingId, bool perBiznesin)
    {
        var titulli = perBiznesin ? "Ke marre nje pagese te re" : "Pagesa u krye me sukses";
        var pershkrimi = perBiznesin
            ? $"Klienti <strong>{counterpartyName}</strong> pagoi {(eshtePagesePlote ? "shumen e plote" : "depoziten (1 dite)")} per <strong>{makina}</strong> me karte."
            : $"Pagesa jote per <strong>{makina}</strong> prane <strong>{counterpartyName}</strong> u krye me sukses me karte.";

        var body = $@"
            <h1 style='color:#111111; font-size:24px; font-weight:800; margin:0 0 6px 0;'>{titulli} ✓</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0 0 24px 0;'>
                Përshëndetje <strong>{emri}</strong>, {pershkrimi}
            </p>

            <div style='border:1px solid #ebebeb; border-radius:16px; padding:24px;'>
                {SectionLabel("Fatura")}
                <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin-top:8px;'>
                  <tr>
                    <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Numri i konfirmimit</td>
                    <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#111111; font-size:13px; font-weight:700; text-align:right;'>{Confirmim(bookingId)}</td>
                  </tr>
                  <tr>
                    <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Menyra</td>
                    <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#111111; font-size:13px; font-weight:700; text-align:right;'>Karte — {(eshtePagesePlote ? "Pagese e plote" : "Depozite (1 dite)")}</td>
                  </tr>
                  <tr>
                    <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#717171; font-size:13px;'>Shuma e paguar</td>
                    <td style='padding:12px 0; border-top:1px solid #ebebeb; color:#0f766e; font-size:15px; font-weight:800; text-align:right;'>{amountPaid}€</td>
                  </tr>
                </table>
            </div>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), $"{titulli} — ERental", "", Wrap(body, titulli));
        await Client.SendEmailAsync(msg);
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
        await Client.SendEmailAsync(msg);
    }


    public async Task SendPasswordCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <p style='color:#484848; font-size:15px; margin:0 0 4px 0;'>Përshëndetje {emri},</p>
            {PlainCodeDisplay(code)}
            <p style='color:#a3a3a3; font-size:12px; line-height:1.6; margin:16px 0 0 0;'>
                Nëse s'e ke kërkuar ti, shpërnfille këtë email.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kodi per fjalekalimin — ERental", "", WrapCode(body, "Kodi yt për ndryshimin e fjalëkalimit"));
        await Client.SendEmailAsync(msg);
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

        await Client.SendEmailAsync(msg);
    }

    // Professional shell for account/business lifecycle emails (verification, welcome) — gold/dark
    // brand accent instead of the teal used on booking emails, deliberately understated.
    private string WrapBrand(string bodyHtml, string preheader = "")
    {
        var preheaderHtml = string.IsNullOrEmpty(preheader) ? "" : $@"
          <div style='display:none; max-height:0; overflow:hidden; mso-hide:all;'>{preheader}</div>";

        return $@"
        {preheaderHtml}
        <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 560px; margin: 0 auto; background:#ffffff; color:#222222;'>

          <div style='height:4px; background:#d97706;'></div>

          <div style='padding:28px 40px 20px 40px;'>
            <span style='font-size:19px; font-weight:800; letter-spacing:-0.4px;'>
              <span style='color:#d97706;'>E</span><span style='color:#111111;'>Rental</span>
            </span>
          </div>

          <div style='padding:8px 40px 40px 40px;'>
              {bodyHtml}
          </div>

          <div style='padding:28px 40px 32px 40px; border-top:1px solid #ebebeb;'>
            <p style='color:#111111; font-size:13px; font-weight:700; margin:0 0 4px 0;'>ERental Albania</p>
            <p style='color:#767676; font-size:12px; line-height:1.6; margin:0;'>
                <a href='mailto:info@erental.store' style='color:#111111; text-decoration:underline;'>info@erental.store</a>
            </p>
          </div>

        </div>";
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

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(adminEmail), $"Kërkesë verifikimi — {companyName}", "", WrapBrand(body, $"{companyName} pret verifikim"));
        await Client.SendEmailAsync(msg);
    }

    public async Task SendCompanyVerifiedAsync(string toEmail, string emri, string companyName)
    {
        var body = $@"
            {CheckBadge()}
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Biznesi u verifikua</h1>
            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje {emri}, <strong>{companyName}</strong> u shqyrtua dhe u verifikua nga ekipi i ERental. Makinat e tua tani shfaqen me shenjën ""I verifikuar"" dhe klientët mund t'i rezervojnë.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Biznesi yt u verifikua — ERental", "", WrapBrand(body, "Je gati të marrësh rezervime"));
        await Client.SendEmailAsync(msg);
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

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Mirë se erdhe në ERental", "", WrapBrand(body, "Llogaria jote është gati"));
        await Client.SendEmailAsync(msg);
    }
}
