using System;
using System.Collections.Generic;
using System.Text;

namespace ERental.Application.Interfaces;

public record RentalContractDto(
    int BookingId,
    string CompanyName, string CompanyNipt, string? CompanyAddress, string? CompanyCity, string? CompanyPhone, string? CompanyEmail,
    string ClientName, string? ClientPhone, string? ClientEmail,
    string CarMakeModel, int CarYear, string? CarPlate, string? CarCategory, string? CarPhotoUrl,
    string DataFillimit, string DataPerfundimit, decimal TotalPrice, decimal? PaidOnline, string PaymentMethodLabel
);

public interface IEmailService
{
    Task SendVerificationCodeAsync(string toEmail, string emri, string code);
    Task SendBookingPendingToClientAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? carPhotoUrl = null);
    Task SendBookingRequestToBusinessAsync(string toEmail, string bizniEmri, string makina, string klientiEmri, string dataFillimit, string dataPerfundimit, string? carPhotoUrl = null);
    Task SendBookingConfirmedAsync(string toEmail, string emri, string makina, string bizniEmri, string dataFillimit, string dataPerfundimit, decimal total, int bookingId, string? companyAddress, string? companyCity, string? companyPhone, string? carPhotoUrl, string? contractUrl = null);
    Task SendBookingCancelledAsync(string toEmail, string emri, string makina, string dataFillimit, string dataPerfundimit, int bookingId, string? carPhotoUrl = null, string? arsyeja = null);
    Task SendPaymentReceiptAsync(string toEmail, string emri, string makina, string counterpartyName, decimal amountPaid, bool eshtePagesePlote, int bookingId, bool perBiznesin, decimal totalPrice, string dataFillimit, string dataPerfundimit);
    Task SendReviewRequestAsync(string toEmail, string emri, string makina, string bizniEmri);
    Task SendPasswordCodeAsync(string toEmail, string emri, string code);
    Task SendContactMessageAsync(string emriDerguesi, string emailDerguesi, string subjekti, string mesazhi);
    Task SendAdminVerificationRequestAsync(string adminEmail, string companyName, int companyId);
    Task SendCompanyVerifiedAsync(string toEmail, string emri, string companyName);
    Task SendWelcomeAsync(string toEmail, string emri);
    string BuildContractHtmlPage(RentalContractDto dto);
}