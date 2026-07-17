using ERental.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Text;

namespace ERental.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config) => _config = config;

    private SendGridClient Client => new(_config["SendGrid:ApiKey"]);
    private EmailAddress From => new(_config["SendGrid:FromEmail"], _config["SendGrid:FromName"]);

    private string Wrap(string title, string bodyHtml) => $@"
        <div style='font-family: Inter, Arial, sans-serif; max-width: 520px; margin: 0 auto; background:#ffffff; border-radius:16px; overflow:hidden; box-shadow:0 8px 30px rgba(0,0,0,0.08);'>

          <div style='background:linear-gradient(135deg,#0f172a,#1e293b); padding:30px; text-align:center;'>
            <div style='font-size:26px; font-weight:800; color:white; letter-spacing:1px;'>
                ERental
            </div>
            <div style='color:#cbd5e1; font-size:13px; margin-top:6px;'>
                Makina me qera, shpejt dhe sigurt
            </div>
          </div>

          <div style='padding:35px 28px;'>
              {bodyHtml}
          </div>

          <div style='background:#f8fafc; padding:18px; text-align:center; border-top:1px solid #e2e8f0;'>
            <p style='color:#64748b; font-size:12px; margin:0;'>
                ERental Albania © Platforma jote e makinave me qera.
            </p>
          </div>

        </div>";

    private string CarImage(string? url) => string.IsNullOrEmpty(url) ? "" : $@"
        <img src='{url}' alt='Makina' style='width:100%; max-height:200px; object-fit:cover; border-radius:14px; display:block; margin-bottom:20px;' />";

    private string DateRangeCard(string dataFillimit, string dataPerfundimit, string accent = "#15803d", string bg = "#f0fdf4", string border = "#86efac") => $@"
        <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin:18px 0;'>
          <tr>
            <td style='background:{bg}; border:1px solid {border}; border-radius:14px; padding:16px 8px; text-align:center; width:44%;'>
                <div style='font-size:10px; color:{accent}; font-weight:700; text-transform:uppercase; letter-spacing:0.5px; margin-bottom:6px;'>Marrja</div>
                <div style='font-size:15px; color:#0f172a; font-weight:800;'>{dataFillimit}</div>
            </td>
            <td style='width:12%; text-align:center; font-size:20px; color:#94a3b8;'>→</td>
            <td style='background:{bg}; border:1px solid {border}; border-radius:14px; padding:16px 8px; text-align:center; width:44%;'>
                <div style='font-size:10px; color:{accent}; font-weight:700; text-transform:uppercase; letter-spacing:0.5px; margin-bottom:6px;'>Dorëzimi</div>
                <div style='font-size:15px; color:#0f172a; font-weight:800;'>{dataPerfundimit}</div>
            </td>
          </tr>
        </table>";

    private string ClientCard(string klientiEmri) => $@"
        <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin-bottom:6px;'>
          <tr>
            <td style='background:#eff6ff; border-radius:14px; padding:14px 16px;'>
              <table role='presentation' cellpadding='0' cellspacing='0'>
                <tr>
                  <td style='width:38px;'>
                    <div style='width:36px;height:36px;border-radius:50%;background:#1e3a8a;color:#ffffff;text-align:center;line-height:36px;font-weight:800;font-size:15px;'>
                      {(string.IsNullOrEmpty(klientiEmri) ? "?" : klientiEmri.Substring(0, 1).ToUpper())}
                    </div>
                  </td>
                  <td style='padding-left:12px;'>
                    <div style='font-size:15px;font-weight:700;color:#0f172a;'>{klientiEmri}</div>
                    <div style='font-size:12px;color:#64748b;'>kërkoi të rezervojë</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
        </table>";

    public async Task SendVerificationCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <h2 style='color:#0f172a; margin-bottom:10px;'>Përshëndetje {emri} 👋</h2>

            <p style='color:#475569; font-size:15px; line-height:1.7;'>
                Përdor kodin më poshtë për të verifikuar email-in tënd dhe për të aktivizuar llogarinë ERental.
            </p>

            <div style='background:#f0fdf4; border:1px solid #86efac; border-radius:14px; padding:25px; text-align:center; margin:25px 0;'>
                <span style='font-size:36px; font-weight:800; letter-spacing:10px; color:#15803d;'>
                    {code}
                </span>
            </div>

            <p style='color:#64748b; font-size:13px; text-align:center;'>
                Ky kod skadon pas 15 minutash.
            </p>";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Kodi yt i verifikimit — ERental",
            "",
            Wrap("Verifikim", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingPendingToClientAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, decimal total, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h2 style='color:#0f172a;'>Rezervimi u dërgua 🚗</h2>

            <p style='color:#475569; font-size:15px; line-height:1.7;'>
                Përshëndetje <strong>{emri}</strong>,
                rezervimi për makinën <strong>{makina}</strong>
                është dërguar me sukses.
            </p>

            {DateRangeCard(dataFillimit, dataPerfundimit)}

            <div style='background:#f8fafc; padding:14px 18px; border-radius:12px; margin:16px 0; text-align:center;'>
                <span style='color:#334155;'>💶 Totali: <strong style='color:#0f172a;'>{total}€</strong></span>
            </div>

            <p style='color:#64748b;font-size:13px;'>
                Biznesi do ta shqyrtojë kërkesën dhe do të njoftohesh për përgjigjen.
            </p>";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Rezervimi yt është në pritje — ERental",
            "",
            Wrap("Pending", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingRequestToBusinessAsync(string toEmail, string bizniEmri, string makina, string klientiEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h2 style='color:#0f172a;'>Kërkesë e re rezervimi 🔔</h2>

            <p style='color:#475569;font-size:15px;line-height:1.7;'>
                Përshëndetje <strong>{bizniEmri}</strong>, ke një kërkesë të re per:
            </p>

            {ClientCard(klientiEmri)}

            <div style='background:#eff6ff;border-radius:12px;padding:14px 18px;text-align:center;margin-bottom:6px;'>
                <span style='color:#1e3a8a;font-weight:700;'>🚗 {makina}</span>
            </div>

            {DateRangeCard(dataFillimit, dataPerfundimit, "#1e3a8a", "#eff6ff", "#bfdbfe")}

            <p style='margin-top:16px;color:#475569;'>
                Hyr në panelin e biznesit për ta miratuar ose refuzuar kërkesën.
            </p>";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Kërkesë e re rezervimi — ERental",
            "",
            Wrap("Request", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h2 style='color:#15803d;'>Rezervimi u konfirmua ✅</h2>

            <p style='color:#475569;font-size:15px;line-height:1.7;'>
                Përshëndetje <strong>{emri}</strong>,
                <strong>{bizniEmri}</strong> konfirmoi rezervimin tënd.
            </p>

            <div style='background:#f0fdf4;border-radius:12px;padding:12px 18px;text-align:center;margin-bottom:6px;'>
                <span style='color:#15803d;font-weight:700;'>🚗 {makina}</span>
            </div>

            {DateRangeCard(dataFillimit, dataPerfundimit)}

            <p style='color:#475569;margin-top:16px;'>
                Mund të shkosh në datën e caktuar për të marrë makinën.
            </p>";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Rezervimi u konfirmua — ERental",
            "",
            Wrap("Confirmed", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendBookingCancelledAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null)
    {
        var body = $@"
            {CarImage(carPhotoUrl)}

            <h2 style='color:#b91c1c;'>Rezervimi u anulua ❌</h2>

            <p style='color:#475569;font-size:15px;line-height:1.7;'>
                Përshëndetje <strong>{emri}</strong>,
                rezervimi yt për makinën <strong>{makina}</strong> është anuluar.
            </p>

            <div style='background:#fef2f2;border-radius:12px;padding:12px 18px;text-align:center;margin-bottom:6px;'>
                <span style='color:#b91c1c;font-weight:700;'>🚗 {makina}</span>
            </div>

            {DateRangeCard(dataFillimit, dataPerfundimit, "#b91c1c", "#fef2f2", "#fecaca")}";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Rezervimi u anulua — ERental",
            "",
            Wrap("Cancelled", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendReviewRequestAsync(string toEmail, string emri, string makina, string bizniEmri)
    {
        var body = $@"
            <h2 style='color:#0f172a;'>Si ishte qeraja? ⭐</h2>

            <p style='color:#475569;font-size:15px;line-height:1.7;'>
                Përshëndetje <strong>{emri}</strong>,
                shpresojmë që qeraja e <strong>{makina}</strong> nga <strong>{bizniEmri}</strong> shkoi mirë.
            </p>

            <p style='color:#475569;font-size:15px;line-height:1.7;'>
                Na ndihmo dhe klientë të tjerë duke lënë një vlerësim — hyr te 'Rezervimet' në ERental.
            </p>";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Le nje vleresim per qerane tende — ERental",
            "",
            Wrap("Review", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendPasswordCodeAsync(string toEmail, string emri, string code)
    {
        var body = $@"
            <h2 style='color:#0f172a; margin-bottom:10px;'>Kodi per fjalekalimin 🔐</h2>

            <p style='color:#475569; font-size:15px; line-height:1.7;'>
                Përshëndetje <strong>{emri}</strong>,
                përdor kodin më poshtë për të vazhduar me ndryshimin e fjalëkalimit tënd.
            </p>

            <div style='background:#f0fdf4; border:1px solid #86efac; border-radius:14px; padding:25px; text-align:center; margin:25px 0;'>
                <span style='font-size:36px; font-weight:800; letter-spacing:10px; color:#15803d;'>
                    {code}
                </span>
            </div>

            <p style='color:#64748b; font-size:13px; text-align:center;'>
                Ky kod skadon pas 15 minutash. Nese s'e ke kerkuar ti, shpernfille kete email.
            </p>";

        var msg = MailHelper.CreateSingleEmail(
            From,
            new EmailAddress(toEmail),
            "Kodi per fjalekalimin — ERental",
            "",
            Wrap("Password", body));

        await Client.SendEmailAsync(msg);
    }


    public async Task SendContactMessageAsync(string emriDerguesi, string emailDerguesi, string subjekti, string mesazhi)
    {
        var body = $@"
            <h2 style='color:#0f172a;'>{subjekti} 📩</h2>

            <p style='color:#475569; font-size:15px; line-height:1.7;'>
                Mesazh i ri nga <strong>{emriDerguesi}</strong> (<a href='mailto:{emailDerguesi}' style='color:#15803d;'>{emailDerguesi}</a>):
            </p>

            <div style='background:#f8fafc; border-radius:12px; padding:18px; margin:16px 0; color:#334155; white-space:pre-line;'>
                {mesazhi}
            </div>

            <p style='color:#64748b; font-size:13px;'>
                Perdor butonin Reply per t'iu pergjigjur direkt dergesit.
            </p>";

        var msg = new SendGrid.Helpers.Mail.SendGridMessage();
        msg.SetFrom(From);
        msg.AddTo(new EmailAddress("info@erental.store"));
        msg.SetReplyTo(new EmailAddress(emailDerguesi, emriDerguesi));
        msg.SetSubject($"{subjekti} — ERental");
        msg.AddContent("text/html", Wrap("Contact", body));

        await Client.SendEmailAsync(msg);
    }
}