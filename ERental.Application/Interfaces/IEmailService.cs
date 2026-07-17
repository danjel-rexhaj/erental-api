using System;
using System.Collections.Generic;
using System.Text;

namespace ERental.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string toEmail, string emri, string code);
    Task SendBookingPendingToClientAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, decimal total, string? carPhotoUrl = null);
    Task SendBookingRequestToBusinessAsync(string toEmail, string bizniEmri, string makina, string klientiEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null);
    Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null);
    Task SendBookingCancelledAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null);
    Task SendReviewRequestAsync(string toEmail, string emri, string makina, string bizniEmri);
    Task SendPasswordCodeAsync(string toEmail, string emri, string code);
    Task SendContactMessageAsync(string emriDerguesi, string emailDerguesi, string subjekti, string mesazhi);
}