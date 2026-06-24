using System.Globalization;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Modules.Contracting.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Contracting.Application.Queries;

/// <summary>CON-C1-6 — داشبوردِ مالیِ پیمان: پیشرفت، سپرده‌های نگه‌داشته، ماندهٔ پیش‌پرداخت، سود.</summary>
public record GetProjectDashboardQuery(int ContractProjectId) : IRequest<ProjectDashboardDto?>;

public record ProjectDashboardDto(
    int ProjectId, string Code, string Title, decimal ContractAmount,
    decimal CumulativeBilled, decimal GrossBilled, decimal NetBilled,
    decimal RetentionHeld, decimal InsuranceHeld, decimal TaxWithheld,
    decimal AdvanceReceived, decimal AdvanceOutstanding,
    decimal ProgressPercent, decimal Profit, int PostedStatementCount);

public class GetProjectDashboardQueryHandler : IRequestHandler<GetProjectDashboardQuery, ProjectDashboardDto?>
{
    private readonly IRepository<ContractProject> _projects;
    private readonly IRepository<ProgressStatement> _statements;
    private readonly IRepository<AdvancePayment> _advances;
    private readonly IRepository<VoucherItem> _voucherItems;
    private readonly ICurrentUserService _user;

    public GetProjectDashboardQueryHandler(IRepository<ContractProject> projects, IRepository<ProgressStatement> statements,
        IRepository<AdvancePayment> advances, IRepository<VoucherItem> voucherItems, ICurrentUserService user)
    { _projects = projects; _statements = statements; _advances = advances; _voucherItems = voucherItems; _user = user; }

    public async Task<ProjectDashboardDto?> Handle(GetProjectDashboardQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var p = await _projects.FindSingleAsync(x => x.Id == req.ContractProjectId && x.CompanyId == companyId, ct);
        if (p is null) return null;

        var posted = (await _statements.FindAsync(
                s => s.CompanyId == companyId && s.ContractProjectId == p.Id && s.Status == StatementStatus.Posted, ct))
            .ToList();

        var cumulativeBilled = posted.Count == 0 ? 0 : posted.Max(s => s.CumulativeGrossWork);
        var grossBilled = posted.Sum(s => s.GrossThisPeriod);
        var netBilled = posted.Sum(s => s.NetPayable);
        var retentionHeld = posted.Sum(s => s.Retention);
        var insuranceHeld = posted.Sum(s => s.Insurance);
        var taxWithheld = posted.Sum(s => s.Tax);

        var advances = await _advances.FindAsync(a => a.CompanyId == companyId && a.ContractProjectId == p.Id, ct);
        var advanceReceived = advances.Sum(a => a.Amount);
        var advanceOutstanding = advances.Sum(a => a.Outstanding);

        var progress = p.ContractAmount > 0 ? Math.Round(cumulativeBilled / p.ContractAmount * 100m, 2) : 0;

        // سود = درآمد/هزینهٔ تگ‌خوردهٔ بُعدِ پروژه (بستانکار − بدهکار روی خطوطِ همان پروژه).
        decimal profit = 0;
        if (p.ProjectDimensionId is int dim)
        {
            var items = await _voucherItems.FindAsync(i => i.ProjectId == dim, ct);
            profit = items.Sum(i => i.Credit) - items.Sum(i => i.Debit);
        }

        return new ProjectDashboardDto(p.Id, p.Code, p.Title, p.ContractAmount,
            cumulativeBilled, grossBilled, netBilled, retentionHeld, insuranceHeld, taxWithheld,
            advanceReceived, advanceOutstanding, progress, profit, posted.Count);
    }
}

/// <summary>CON-C1-6 — فهرستِ ضمانت‌نامه‌ها با هشدارِ انقضا (روزهای ماندهٔ تا انقضا).</summary>
public record GetGuaranteesQuery(int? ContractProjectId = null, bool ActiveOnly = true) : IRequest<List<GuaranteeDto>>;

public record GuaranteeDto(
    int Id, int ContractProjectId, GuaranteeType Type, string Bank, decimal Amount,
    string IssueDate, string ExpiryDate, GuaranteeStatus Status, int? DaysToExpiry, bool IsExpiringSoon);

public class GetGuaranteesQueryHandler : IRequestHandler<GetGuaranteesQuery, List<GuaranteeDto>>
{
    private const int SoonThresholdDays = 30;
    private readonly IRepository<Guarantee> _guarantees;
    private readonly ICurrentUserService _user;
    public GetGuaranteesQueryHandler(IRepository<Guarantee> guarantees, ICurrentUserService user)
    { _guarantees = guarantees; _user = user; }

    public async Task<List<GuaranteeDto>> Handle(GetGuaranteesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = (await _guarantees.FindAsync(g => g.CompanyId == companyId, ct))
            .Where(g => req.ContractProjectId is null || g.ContractProjectId == req.ContractProjectId)
            .Where(g => !req.ActiveOnly || g.Status == GuaranteeStatus.Active)
            .ToList();

        var today = DateTime.Today;
        return list.Select(g =>
        {
            int? days = ToGregorian(g.ExpiryDate) is DateTime exp ? (int)(exp - today).TotalDays : null;
            var soon = g.Status == GuaranteeStatus.Active && days is >= 0 and <= SoonThresholdDays;
            return new GuaranteeDto(g.Id, g.ContractProjectId, g.Type, g.Bank, g.Amount,
                g.IssueDate, g.ExpiryDate, g.Status, days, soon);
        }).OrderBy(g => g.DaysToExpiry ?? int.MaxValue).ToList();
    }

    /// <summary>تبدیلِ تاریخِ شمسیِ «YYYY/MM/DD» به میلادی (برای محاسبهٔ روزهای ماندهٔ انقضا).</summary>
    private static DateTime? ToGregorian(string shamsi)
    {
        var pp = (shamsi ?? "").Replace('-', '/').Split('/');
        if (pp.Length < 3) return null;
        if (!int.TryParse(pp[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(pp[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ||
            !int.TryParse(pp[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)) return null;
        if (y < 1 || m is < 1 or > 12 || d is < 1 or > 31) return null;
        try { return new PersianCalendar().ToDateTime(y, m, d, 0, 0, 0, 0); } catch { return null; }
    }
}
