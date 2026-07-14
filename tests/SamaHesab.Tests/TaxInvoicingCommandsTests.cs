using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Common.Events;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Application;
using SamaHesab.Modules.TaxInvoicing.Application.Commands;
using SamaHesab.Modules.TaxInvoicing.Application.EventHandlers;
using SamaHesab.Modules.TaxInvoicing.Application.Queries;
using SamaHesab.Modules.TaxInvoicing.Crypto;
using SamaHesab.Modules.TaxInvoicing.Domain;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-2 (سامانهٔ مودیان) — Command/Query/EventHandlerهایِ Application، با هارنسِ فایل‌محلیِ
/// معمولِ این پروژه (FakeRepo/FakeUow/FakeUser).</summary>
public class TaxInvoicingCommandsTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static void SetId(T e, int value)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop != null) prop.SetValue(e, System.Convert.ChangeType(value, prop.PropertyType));
        }
        public Task AddAsync(T e, CancellationToken ct = default)
        { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(e => (int)(typeof(T).GetProperty("Id")!.GetValue(e) ?? 0) == id));
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { foreach (var x in es.ToList()) Items.Remove(x); }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new System.NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public int? SalespersonPartyId => null;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    /// <summary>کلیدِ آزمایشیِ در-حافظه — بدونِ فایلِ واقعیِ گواهی (طبقِ توضیحِ IModianCertificateProvider).</summary>
    private sealed class FakeCertProvider : IModianCertificateProvider
    {
        public RSA Key = RSA.Create(2048);
        public RSA LoadSigningKey(ModianSettings settings) => Key;
    }

    private sealed class FakeApiClient : IModianApiClient
    {
        public Result<string> NonceResult = Result<string>.Success("nonce");
        public Result<string> PublicKeyResult = Result<string>.Success("");
        public Result<ModianSubmitResult> SubmitResult = Result<ModianSubmitResult>.Success(new ModianSubmitResult("REF-1"));
        public Result<ModianInquiryResult> InquiryResult = Result<ModianInquiryResult>.Success(new ModianInquiryResult("Accepted", null, null));
        public string? LastSubmittedJwe;

        public Task<Result<string>> GetNonceAsync(bool useSandbox, CancellationToken ct = default) => Task.FromResult(NonceResult);
        public Task<Result<string>> GetServerPublicKeyPemAsync(bool useSandbox, CancellationToken ct = default) => Task.FromResult(PublicKeyResult);
        public Task<Result<ModianSubmitResult>> SubmitInvoiceAsync(string jwe, bool useSandbox, CancellationToken ct = default)
        { LastSubmittedJwe = jwe; return Task.FromResult(SubmitResult); }
        public Task<Result<ModianInquiryResult>> InquiryByReferenceNumberAsync(string referenceNumber, bool useSandbox, CancellationToken ct = default) => Task.FromResult(InquiryResult);
    }

    private static string ExportPublicKeyPem(RSA rsa) => rsa.ExportSubjectPublicKeyInfoPem();

    // ── QueueElectronicInvoiceOnSalesPostedHandler ──

    [Fact]
    public async Task Queue_Creates_Pending_Submission_When_Settings_Enabled()
    {
        var submissions = new FakeRepo<ElectronicInvoiceSubmission>();
        var settings = new FakeRepo<ModianSettings>();
        var s = ModianSettings.Create(1);
        s.Update("TM-1", true, "c:\\cert.pfx", "pw", enabled: true);
        await settings.AddAsync(s);

        var handler = new QueueElectronicInvoiceOnSalesPostedHandler(submissions, settings, new FakeUow());
        var evt = new SalesInvoicePostedEvent(invoiceId: 42, companyId: 1, customerId: 5, amount: 100_000, userId: 1);

        await handler.Handle(new DomainEventNotification<SalesInvoicePostedEvent>(evt), default);

        var sub = Assert.Single(submissions.Items);
        Assert.Equal(42, sub.SalesInvoiceId);
        Assert.Equal(SubmissionStatus.Pending, sub.Status);
    }

    [Fact]
    public async Task Queue_Does_Nothing_When_Settings_Missing_Or_Disabled()
    {
        var submissions = new FakeRepo<ElectronicInvoiceSubmission>();
        var settings = new FakeRepo<ModianSettings>();   // خالی — ماژول پیکربندی نشده

        var handler = new QueueElectronicInvoiceOnSalesPostedHandler(submissions, settings, new FakeUow());
        var evt = new SalesInvoicePostedEvent(42, 1, 5, 100_000, 1);

        await handler.Handle(new DomainEventNotification<SalesInvoicePostedEvent>(evt), default);

        Assert.Empty(submissions.Items);
    }

    [Fact]
    public async Task Queue_Is_Idempotent_For_The_Same_Invoice()
    {
        var submissions = new FakeRepo<ElectronicInvoiceSubmission>();
        var settings = new FakeRepo<ModianSettings>();
        var s = ModianSettings.Create(1);
        s.Update("TM-1", true, "c:\\cert.pfx", "pw", true);
        await settings.AddAsync(s);
        var handler = new QueueElectronicInvoiceOnSalesPostedHandler(submissions, settings, new FakeUow());
        var evt = new SalesInvoicePostedEvent(42, 1, 5, 100_000, 1);

        await handler.Handle(new DomainEventNotification<SalesInvoicePostedEvent>(evt), default);
        await handler.Handle(new DomainEventNotification<SalesInvoicePostedEvent>(evt), default);

        Assert.Single(submissions.Items);
    }

    // ── SendElectronicInvoiceCommand ──

    private static (SendElectronicInvoiceCommandHandler Handler, FakeRepo<ElectronicInvoiceSubmission> Submissions,
        FakeRepo<ModianSettings> Settings, FakeCertProvider CertProvider, FakeApiClient Api) BuildSendHarness()
    {
        var submissions = new FakeRepo<ElectronicInvoiceSubmission>();
        var settings = new FakeRepo<ModianSettings>();
        var invoices = new FakeRepo<SalesInvoice>();
        var certProvider = new FakeCertProvider();
        var api = new FakeApiClient { PublicKeyResult = Result<string>.Success(ExportPublicKeyPem(RSA.Create(2048))) };
        var crypto = new ModianCryptoService();

        var handler = new SendElectronicInvoiceCommandHandler(
            submissions, settings, invoices, new FakeUow(), crypto, certProvider, api);

        return (handler, submissions, settings, certProvider, api);
    }

    [Fact]
    public async Task Send_Fails_When_Settings_Not_Configured()
    {
        var (handler, submissions, _, _, _) = BuildSendHarness();
        var sub = ElectronicInvoiceSubmission.Create(1, 42);
        await submissions.AddAsync(sub);

        var res = await handler.Handle(new SendElectronicInvoiceCommand(sub.Id), default);

        Assert.False(res.Succeeded);
        Assert.Equal(SubmissionStatus.Error, submissions.Items[0].Status);
    }

    [Fact]
    public async Task Send_Succeeds_And_Marks_Sent_With_ReferenceNumber()
    {
        var (handler, submissions, settings, _, api) = BuildSendHarness();
        var s = ModianSettings.Create(1);
        s.Update("TM-1", true, "c:\\cert.pfx", "pw", true);
        await settings.AddAsync(s);
        var invoice = SalesInvoice.Create(1, 1, 1, "F000001", "1405/04/15", 10, 1);
        await GetInvoicesRepo(handler).AddAsync(invoice);
        var sub = ElectronicInvoiceSubmission.Create(1, invoice.Id);
        await submissions.AddAsync(sub);

        var res = await handler.Handle(new SendElectronicInvoiceCommand(sub.Id), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.Equal(SubmissionStatus.Sent, sub.Status);
        Assert.Equal("REF-1", sub.ReferenceNumber);
        Assert.NotNull(api.LastSubmittedJwe);
        Assert.Equal(5, api.LastSubmittedJwe!.Split('.').Length);   // JWE compactِ ۵بخشی
    }

    [Fact]
    public async Task Send_Marks_Error_When_Organization_Rejects()
    {
        var (handler, submissions, settings, _, api) = BuildSendHarness();
        api.SubmitResult = Result<ModianSubmitResult>.Failure("سازمان رد کرد: کدِ کالای نامعتبر");
        var s = ModianSettings.Create(1);
        s.Update("TM-1", true, "c:\\cert.pfx", "pw", true);
        await settings.AddAsync(s);
        var invoice = SalesInvoice.Create(1, 1, 1, "F000002", "1405/04/15", 10, 1);
        await GetInvoicesRepo(handler).AddAsync(invoice);
        var sub = ElectronicInvoiceSubmission.Create(1, invoice.Id);
        await submissions.AddAsync(sub);

        var res = await handler.Handle(new SendElectronicInvoiceCommand(sub.Id), default);

        Assert.False(res.Succeeded);
        Assert.Equal(SubmissionStatus.Error, sub.Status);
        Assert.Equal(1, sub.RetryCount);
        Assert.Contains("کدِ کالای نامعتبر", sub.ErrorMessage);
    }

    /// <summary>هارنسِ ساده اجازه نمی‌دهد به فیلدِ خصوصیِ invoices برسیم؛ از رفلکشن استفاده می‌کنیم
    /// تا در تست بتوانیم مستقیماً فاکتور را seed کنیم.</summary>
    private static FakeRepo<SalesInvoice> GetInvoicesRepo(SendElectronicInvoiceCommandHandler handler)
    {
        var field = typeof(SendElectronicInvoiceCommandHandler)
            .GetField("_invoices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (FakeRepo<SalesInvoice>)field.GetValue(handler)!;
    }

    // ── RetryPendingElectronicInvoicesCommand ──

    private sealed class ForwardingMediator : IMediator
    {
        public SendElectronicInvoiceCommandHandler? SendHandler;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is SendElectronicInvoiceCommand cmd && SendHandler != null)
                return (Task<TResponse>)(object)SendHandler.Handle(cmd, ct);
            throw new System.NotImplementedException();
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    [Fact]
    public async Task Retry_Processes_Pending_And_Error_Submissions_And_Reports_Summary()
    {
        var (sendHandler, submissions, settings, _, api) = BuildSendHarness();
        var s = ModianSettings.Create(1);
        s.Update("TM-1", true, "c:\\cert.pfx", "pw", true);
        await settings.AddAsync(s);

        var invoicesRepo = GetInvoicesRepo(sendHandler);
        var inv1 = SalesInvoice.Create(1, 1, 1, "F1", "1405/04/15", 10, 1);
        var inv2 = SalesInvoice.Create(1, 1, 1, "F2", "1405/04/15", 10, 1);
        await invoicesRepo.AddAsync(inv1);
        await invoicesRepo.AddAsync(inv2);
        var sub1 = ElectronicInvoiceSubmission.Create(1, inv1.Id);       // Pending
        var sub2 = ElectronicInvoiceSubmission.Create(1, inv2.Id);
        sub2.MarkError("قبلاً شکست خورد");                              // Error → هم باید تلاشِ مجدد بخورد
        var sub3 = ElectronicInvoiceSubmission.Create(1, 999);
        sub3.MarkAccepted("uid");                                        // Accepted → نباید دوباره پردازش شود
        await submissions.AddAsync(sub1);
        await submissions.AddAsync(sub2);
        await submissions.AddAsync(sub3);

        var mediator = new ForwardingMediator { SendHandler = sendHandler };
        var retryHandler = new RetryPendingElectronicInvoicesCommandHandler(submissions, new FakeUser(), mediator);

        var res = await retryHandler.Handle(new RetryPendingElectronicInvoicesCommand(), default);

        Assert.True(res.Succeeded);
        Assert.Equal(2, res.Value!.Attempted);   // فقط sub1/sub2 (Pending/Error) — sub3 دست‌نخورده
        Assert.Equal(2, res.Value.Succeeded);
        Assert.Equal(SubmissionStatus.Sent, sub1.Status);
        Assert.Equal(SubmissionStatus.Sent, sub2.Status);
        Assert.Equal(SubmissionStatus.Accepted, sub3.Status);   // دست‌نخورده ماند
    }

    // ── GetElectronicInvoiceSubmissionsQuery ──

    [Fact]
    public async Task GetSubmissions_Filters_By_Company_And_Status_NewestFirst()
    {
        var submissions = new FakeRepo<ElectronicInvoiceSubmission>();
        var s1 = ElectronicInvoiceSubmission.Create(1, 1);
        var s2 = ElectronicInvoiceSubmission.Create(1, 2);
        s2.MarkError("خطا");
        var otherCompany = ElectronicInvoiceSubmission.Create(2, 3);
        await submissions.AddAsync(s1);
        await submissions.AddAsync(s2);
        await submissions.AddAsync(otherCompany);

        var handler = new GetElectronicInvoiceSubmissionsQueryHandler(submissions, new FakeUser());
        var all = await handler.Handle(new GetElectronicInvoiceSubmissionsQuery(), default);
        var onlyErrors = await handler.Handle(new GetElectronicInvoiceSubmissionsQuery(Status: SubmissionStatus.Error), default);

        Assert.Equal(2, all.Count);   // فقط شرکتِ ۱
        Assert.Single(onlyErrors);
        Assert.Equal(s2.Id, onlyErrors[0].Id);
    }

    // ── SaveModianSettingsCommand ──

    [Fact]
    public async Task SaveSettings_Validator_Requires_TaxMemoryId_And_Cert_When_Enabled()
    {
        var validator = new SaveModianSettingsCommandValidator();

        var invalid = validator.Validate(new SaveModianSettingsCommand(null, true, null, null, Enabled: true));
        var valid = validator.Validate(new SaveModianSettingsCommand("TM-1", true, "c:\\cert.pfx", "pw", Enabled: true));
        var validDisabled = validator.Validate(new SaveModianSettingsCommand(null, true, null, null, Enabled: false));

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
        Assert.True(validDisabled.IsValid);
    }

    [Fact]
    public async Task SaveSettings_Creates_Then_Updates_Single_Row_Per_Company()
    {
        var settings = new FakeRepo<ModianSettings>();
        var handler = new SaveModianSettingsCommandHandler(settings, new FakeUow(), new FakeUser());

        await handler.Handle(new SaveModianSettingsCommand("TM-1", true, "c:\\a.pfx", "pw", true), default);
        await handler.Handle(new SaveModianSettingsCommand("TM-2", false, "c:\\b.pfx", "pw2", true), default);

        var row = Assert.Single(settings.Items);
        Assert.Equal("TM-2", row.TaxMemoryId);
        Assert.False(row.UseSandbox);
    }

    // ── GetModianSettingsQuery ──

    [Fact]
    public async Task GetSettings_Returns_Defaults_When_Not_Configured()
    {
        var settings = new FakeRepo<ModianSettings>();
        var handler = new GetModianSettingsQueryHandler(settings, new FakeUser());

        var dto = await handler.Handle(new GetModianSettingsQuery(), default);

        Assert.Null(dto.TaxMemoryId);
        Assert.True(dto.UseSandbox);
        Assert.False(dto.Enabled);
    }

    [Fact]
    public async Task GetSettings_Returns_Saved_Row_For_Current_Company()
    {
        var settings = new FakeRepo<ModianSettings>();
        var s = ModianSettings.Create(1);
        s.Update("TM-9", false, "c:\\x.pfx", "secret", true);
        await settings.AddAsync(s);
        var handler = new GetModianSettingsQueryHandler(settings, new FakeUser());

        var dto = await handler.Handle(new GetModianSettingsQuery(), default);

        Assert.Equal("TM-9", dto.TaxMemoryId);
        Assert.False(dto.UseSandbox);
        Assert.True(dto.Enabled);
    }
}
