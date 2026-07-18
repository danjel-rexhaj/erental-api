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

    private string Wrap(string bodyHtml) => $@"
        <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 560px; margin: 0 auto; background:#ffffff; color:#222222;'>

          <div style='padding:32px 40px 24px 40px; border-bottom:1px solid #ebebeb;'>
            <span style='font-size:20px; font-weight:700; color:#111111; letter-spacing:-0.3px;'>ERental</span>
          </div>

          <div style='padding:40px;'>
              {bodyHtml}
          </div>

          <div style='padding:24px 40px; border-top:1px solid #ebebeb;'>
            <p style='color:#767676; font-size:12px; line-height:1.6; margin:0;'>
                ERental Albania · Platforma e makinave me qera<br/>
                <a href='mailto:info@erental.store' style='color:#767676; text-decoration:underline;'>info@erental.store</a>
            </p>
          </div>

        </div>";

    private string CarImage(string? url) => string.IsNullOrEmpty(url) ? "" : $@"
        <img src='{url}' alt='Makina' style='width:100%; max-height:220px; object-fit:cover; border-radius:12px; display:block; margin-bottom:24px;' />";

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
        <div style='background:#f7f7f7; border-radius:12px; padding:28px; text-align:center; margin:28px 0;'>
            <span style='font-size:32px; font-weight:700; letter-spacing:8px; color:#111111;'>{code}</span>
        </div>
        <p style='color:#717171; font-size:13px; text-align:center; margin:0;'>Ky kod skadon pas 15 minutash.</p>";

    public async Task SendVerificationCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Verifiko email-in tënd</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje {emri}, përdor kodin më poshtë për të verifikuar email-in dhe për të aktivizuar llogarinë ERental.
            </p>

            {CodeBox(code)}";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kodi yt i verifikimit — ERental", "", Wrap(body));
        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingPendingToClientAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, decimal total, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Rezervimi u dërgua</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje <strong>{emri}</strong>, rezervimi për <strong>{makina}</strong> u dërgua me sukses.
            </p>

            {DetailsTable(("Marrja", dataFillimit), ("Dorëzimi", dataPerfundimit), ("Totali", $"{total}€"))}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Biznesi do ta shqyrtojë kërkesën dhe do të njoftohesh për përgjigjen.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi yt është në pritje — ERental", "", Wrap(body));
        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingRequestToBusinessAsync(string toEmail, string bizniEmri, string makina, string klientiEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 20px 0;'>Kërkesë e re rezervimi</h1>

            {PersonRow(klientiEmri, $"kërkoi të rezervojë {makina}")}

            {DetailsTable(("Makina", makina), ("Marrja", dataFillimit), ("Dorëzimi", dataPerfundimit))}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Përshëndetje {bizniEmri}, hyr në panelin e biznesit për ta miratuar ose refuzuar kërkesën.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kërkesë e re rezervimi — ERental", "", Wrap(body));
        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Rezervimi u konfirmua</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje <strong>{emri}</strong>, <strong>{bizniEmri}</strong> konfirmoi rezervimin tënd.
            </p>

            {DetailsTable(("Makina", makina), ("Marrja", dataFillimit), ("Dorëzimi", dataPerfundimit))}

            <p style='color:#717171; font-size:13px; line-height:1.6; margin:0;'>
                Mund të shkosh në datën e caktuar për të marrë makinën.
            </p>";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi u konfirmua — ERental", "", Wrap(body));
        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingCancelledAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null, string? arsyeja = null)
    {
        var reasonBlock = string.IsNullOrWhiteSpace(arsyeja) ? "" : $@"
            <div style='background:#f7f7f7; border-radius:12px; padding:16px; margin:16px 0 0 0;'>
                <p style='color:#717171; font-size:12px; font-weight:700; text-transform:uppercase; letter-spacing:0.5px; margin:0 0 6px 0;'>Arsyeja</p>
                <p style='color:#222222; font-size:14px; line-height:1.6; margin:0;'>{arsyeja}</p>
            </div>";

        var body = $@"
            {CarImage(carPhotoUrl)}

            <h1 style='color:#111111; font-size:22px; font-weight:700; margin:0 0 16px 0;'>Rezervimi u anulua</h1>

            <p style='color:#484848; font-size:15px; line-height:1.7; margin:0;'>
                Përshëndetje <strong>{emri}</strong>, rezervimi yt për <strong>{makina}</strong> është anuluar.
            </p>

            {DetailsTable(("Makina", makina), ("Marrja", dataFillimit), ("Dorëzimi", dataPerfundimit))}
            {reasonBlock}";

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Rezervimi u anulua — ERental", "", Wrap(body));
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

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Le nje vleresim per qerane tende — ERental", "", Wrap(body));
        await Client.SendEmailAsync(msg);
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

        var msg = MailHelper.CreateSingleEmail(From, new EmailAddress(toEmail), "Kodi per fjalekalimin — ERental", "", Wrap(body));
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
}
