using System;
using System.Collections.Generic;
using System.Text;

namespace ERental.Application.Interfaces;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string toEmail, string emri, string code);
    Task SendBookingPendingToClientAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? carPhotoUrl = null);
    Task SendBookingRequestToBusinessAsync(string toEmail, string bizniEmri, string makina, string klientiEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null);
    Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? companyAddress = null, string? companyCity = null, string? companyPhone = null, string? carPhotoUrl = null);
    Task SendBookingCancelledAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, int bookingId, string? carPhotoUrl = null, string? arsyeja = null);
    Task SendPaymentReceiptAsync(string toEmail, string emri, string makina, string counterpartyName, decimal amountPaid, bool eshtePagesePlote, int bookingId, bool perBiznesin);
    Task SendReviewRequestAsync(string toEmail, string emri, string makina, string bizniEmri);
    Task SendPasswordCodeAsync(string toEmail, string emri, string code);
    Task SendContactMessageAsync(string emriDerguesi, string emailDerguesi, string subjekti, string mesazhi);
    Task SendAdminVerificationRequestAsync(string adminEmail, string companyName, int companyId);
    Task SendCompanyVerifiedAsync(string toEmail, string emri, string companyName);
    Task SendWelcomeAsync(string toEmail, string emri);
}