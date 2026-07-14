using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Crypto;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Commands;

/// <summary>
/// ارسالِ یک رکوردِ صف‌شده به سامانهٔ مودیان: امضا (JWS) → رمزنگاری با کلیدِ عمومیِ سرور (JWE) →
/// POST. هر شکستی (تنظیماتِ ناقص، گواهیِ نامعتبر، خطایِ شبکه، ردِ سازمان) رکورد را <c>Error</c>
/// می‌کند (قابلِ‌تلاشِ‌مجدد با <see cref="RetryPendingElectronicInvoicesCommand"/>)، نه استثنایِ خام.
/// <para>⚠️ <see cref="BuildInvoicePayloadJson"/> یک payloadِ **حداقلی و placeholder** است — نگاشتِ
/// دقیقِ فیلدهایِ الزامیِ استانداردِ سازمان (اقلام/مالیات/شناسهٔ کالایِ رسمی و...) نیازِ مستنداتِ رسمیِ
/// tp.tax.gov.ir دارد که هنوز در دسترس نیست؛ فقط اسکلتِ درست (امضا→رمزنگاری→ارسال→وضعیت) اینجا کامل
/// و تست‌شده است.</para>
/// </summary>
public record SendElectronicInvoiceCommand(int SubmissionId) : IRequest<Result>;

public class SendElectronicInvoiceCommandHandler : IRequestHandler<SendElectronicInvoiceCommand, Result>
{
    private readonly IRepository<ElectronicInvoiceSubmission> _submissions;
    private readonly IRepository<ModianSettings> _settings;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IUnitOfWork _uow;
    private readonly IModianCryptoService _crypto;
    private readonly IModianCertificateProvider _certProvider;
    private readonly IModianApiClient _api;

    public SendElectronicInvoiceCommandHandler(
        IRepository<ElectronicInvoiceSubmission> submissions, IRepository<ModianSettings> settings,
        IRepository<SalesInvoice> invoices, IUnitOfWork uow,
        IModianCryptoService crypto, IModianCertificateProvider certProvider, IModianApiClient api)
    {
        _submissions = submissions; _settings = settings; _invoices = invoices; _uow = uow;
        _crypto = crypto; _certProvider = certProvider; _api = api;
    }

    public async Task<Result> Handle(SendElectronicInvoiceCommand req, CancellationToken ct)
    {
        var sub = await _submissions.GetByIdAsync(req.SubmissionId, ct);
        if (sub is null) return Result.Failure("رکوردِ ارسال یافت نشد.");

        var settings = await _settings.FindSingleAsync(s => s.CompanyId == sub.CompanyId, ct);
        if (settings is null || !settings.IsConfigured)
            return await Fail(sub, "تنظیماتِ سامانهٔ مودیان کامل نیست (شناسهٔ حافظهٔ مالیاتی/گواهی).", ct);

        var invoice = await _invoices.GetByIdAsync(sub.SalesInvoiceId, ct);
        if (invoice is null)
            return await Fail(sub, "فاکتورِ فروشِ مرتبط یافت نشد.", ct);

        RSA signingKey;
        try { signingKey = _certProvider.LoadSigningKey(settings); }
        catch (Exception ex) { return await Fail(sub, $"بارگذاریِ گواهیِ دیجیتال ناموفق بود: {ex.Message}", ct); }

        var pubKeyRes = await _api.GetServerPublicKeyPemAsync(settings.UseSandbox, ct);
        if (!pubKeyRes.Succeeded)
            return await Fail(sub, pubKeyRes.ErrorMessage, ct);

        RSA serverKey;
        try
        {
            serverKey = RSA.Create();
            serverKey.ImportFromPem(pubKeyRes.Value);
        }
        catch (Exception ex) { return await Fail(sub, $"کلیدِ عمومیِ سرور نامعتبر بود: {ex.Message}", ct); }

        var payloadJson = BuildInvoicePayloadJson(invoice, settings);
        var jws = _crypto.CreateJws(payloadJson, signingKey);
        var jwe = _crypto.CreateJwe(jws, serverKey);

        var submitRes = await _api.SubmitInvoiceAsync(jwe, settings.UseSandbox, ct);
        if (!submitRes.Succeeded)
            return await Fail(sub, submitRes.ErrorMessage, ct);

        sub.MarkSent(submitRes.Value!.ReferenceNumber);
        _submissions.Update(sub);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> Fail(ElectronicInvoiceSubmission sub, string message, CancellationToken ct)
    {
        sub.MarkError(message);
        _submissions.Update(sub);
        await _uow.SaveChangesAsync(ct);
        return Result.Failure(message);
    }

    /// <summary>⚠️ placeholder — طبقِ مستنداتِ رسمی بازنویسی خواهد شد (نگاهِ کلاسِ بالا).</summary>
    private static string BuildInvoicePayloadJson(SalesInvoice invoice, ModianSettings settings)
    {
        var dto = new
        {
            taxMemoryId = settings.TaxMemoryId,
            invoiceNumber = invoice.InvoiceNumber,
            invoiceDate = invoice.InvoiceDate,
            totalAmount = invoice.GrandTotal,
            totalTax = invoice.TotalTax,
        };
        return JsonSerializer.Serialize(dto);
    }
}
