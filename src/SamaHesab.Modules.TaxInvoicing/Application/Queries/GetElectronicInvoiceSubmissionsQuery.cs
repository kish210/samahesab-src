using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Queries;

/// <summary>فهرستِ صورتحساب‌هایِ الکترونیکی برایِ صفحهٔ لیست (فازِ UI) — جدیدترین اول.</summary>
public record GetElectronicInvoiceSubmissionsQuery(SubmissionStatus? Status = null, int MaxRows = 500)
    : IRequest<List<ElectronicInvoiceSubmissionDto>>;

public record ElectronicInvoiceSubmissionDto(
    int Id, int SalesInvoiceId, string Status, string? UniqueTaxId, string? ReferenceNumber,
    string? ErrorMessage, int RetryCount, DateTime? SentAt, DateTime CreatedAt);

public class GetElectronicInvoiceSubmissionsQueryHandler
    : IRequestHandler<GetElectronicInvoiceSubmissionsQuery, List<ElectronicInvoiceSubmissionDto>>
{
    private readonly IRepository<ElectronicInvoiceSubmission> _submissions;
    private readonly ICurrentUserService _user;

    public GetElectronicInvoiceSubmissionsQueryHandler(
        IRepository<ElectronicInvoiceSubmission> submissions, ICurrentUserService user)
    { _submissions = submissions; _user = user; }

    public async Task<List<ElectronicInvoiceSubmissionDto>> Handle(
        GetElectronicInvoiceSubmissionsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var rows = await _submissions.FindAsync(s => s.CompanyId == companyId, ct);

        return rows
            .Where(s => req.Status == null || s.Status == req.Status)
            .OrderByDescending(s => s.CreatedAt)
            .Take(req.MaxRows <= 0 ? 500 : req.MaxRows)
            .Select(s => new ElectronicInvoiceSubmissionDto(
                s.Id, s.SalesInvoiceId, s.Status.ToString(), s.UniqueTaxId, s.ReferenceNumber,
                s.ErrorMessage, s.RetryCount, s.SentAt, s.CreatedAt))
            .ToList();
    }
}
