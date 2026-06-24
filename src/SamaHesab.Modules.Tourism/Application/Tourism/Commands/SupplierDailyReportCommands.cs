using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application.Commands;

/// <summary>
/// TUR-C1-5 — ساخت/به‌روزرسانیِ گزارشِ روزانهٔ تأمین‌کننده (سندی که برایش می‌فرستیم).
/// جمعِ فروش‌های آن روزِ آن تأمین‌کننده: تعدادِ خط/مسافر و TotalCost (= برداشتِ ودیعهٔ آن روز). idempotent.
/// </summary>
public record GenerateSupplierDailyReportCommand(int SupplierPartyId, string Date) : IRequest<Result<int>>;

public class GenerateSupplierDailyReportCommandHandler
    : IRequestHandler<GenerateSupplierDailyReportCommand, Result<int>>
{
    private readonly IRepository<TourismSale> _sales;
    private readonly IRepository<TourismSaleLine> _lines;
    private readonly IRepository<SalePassenger> _passengers;
    private readonly IRepository<SupplierDailyReport> _reports;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public GenerateSupplierDailyReportCommandHandler(IRepository<TourismSale> sales, IRepository<TourismSaleLine> lines,
        IRepository<SalePassenger> passengers, IRepository<SupplierDailyReport> reports,
        IUnitOfWork uow, ICurrentUserService user)
    { _sales = sales; _lines = lines; _passengers = passengers; _reports = reports; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(GenerateSupplierDailyReportCommand req, CancellationToken ct)
    {
        if (req.SupplierPartyId <= 0) return Result<int>.Failure("تأمین‌کننده الزامی است.");
        if (string.IsNullOrWhiteSpace(req.Date)) return Result<int>.Failure("تاریخ الزامی است.");
        var companyId = _user.CompanyId ?? 1;

        // فروش‌های آن روز → خطوطِ همان تأمین‌کننده.
        var saleIds = (await _sales.FindAsync(s => s.CompanyId == companyId && s.Date == req.Date, ct))
            .Select(s => s.Id).ToHashSet();
        // فیلترِ فروشِ همین شرکت در خودِ کوئری (TourismSaleLine بدونِ CompanyId است).
        var lines = (await _lines.FindAsync(
            l => l.SupplierPartyId == req.SupplierPartyId && saleIds.Contains(l.SaleId), ct)).ToList();

        var totalCost = lines.Sum(l => l.Quantity * l.UnitCost);
        var lineIds = lines.Select(l => l.Id).ToHashSet();
        var paxCount = lineIds.Count == 0 ? 0
            : (await _passengers.FindAsync(p => lineIds.Contains(p.SaleLineId), ct)).Count;

        var report = await _reports.FindSingleAsync(
            r => r.CompanyId == companyId && r.SupplierPartyId == req.SupplierPartyId && r.Date == req.Date, ct);
        if (report is null)   // idempotent: اگر قبلاً ساخته شده، همان برمی‌گردد (بازتولیدِ خودکار نمی‌کنیم).
        {
            report = SupplierDailyReport.Create(companyId, req.SupplierPartyId, req.Date, totalCost, lines.Count, paxCount);
            await _reports.AddAsync(report, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return Result<int>.Success(report.Id);
    }
}

/// <summary>
/// TUR-C1-5 — آشتیِ گزارشِ روزانه: ثبتِ مبلغِ کسرِ واقعیِ تأمین‌کننده؛ اگر با برداشتِ ثبت‌شدهٔ ما فرق داشت،
/// سندِ تعدیل (ودیعه ↔ حسابِ اختلاف) زده و گزارش Reconciled می‌شود.
/// </summary>
public record ReconcileSupplierDailyReportCommand(
    int ReportId, decimal SupplierDeductedAmount, int BranchId, int FiscalYearId,
    string Date, string? Note = null) : IRequest<Result<int>>;

public class ReconcileSupplierDailyReportCommandHandler
    : IRequestHandler<ReconcileSupplierDailyReportCommand, Result<int>>
{
    private readonly IRepository<SupplierDailyReport> _reports;
    private readonly IRepository<TourismSetting> _settings;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public ReconcileSupplierDailyReportCommandHandler(IRepository<SupplierDailyReport> reports,
        IRepository<TourismSetting> settings, IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears,
        IUnitOfWork uow, ICurrentUserService user)
    { _reports = reports; _settings = settings; _vouchers = vouchers; _fiscalYears = fiscalYears; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(ReconcileSupplierDailyReportCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var report = await _reports.FindSingleAsync(r => r.Id == req.ReportId && r.CompanyId == companyId, ct);
        if (report is null) return Result<int>.Failure("گزارشِ روزانه یافت نشد.");
        if (report.Status == DailyReportStatus.Reconciled) return Result<int>.Failure("این گزارش قبلاً آشتی شده.");

        var diff = req.SupplierDeductedAmount - report.TotalCost;   // +: تأمین‌کننده بیشتر کسر کرده
        int? adjVoucherId = null;

        if (Math.Abs(diff) >= 0.01m)
        {
            var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
            if (set?.SupplierDepositAccountId is null || set.DepositDifferenceAccountId is null)
                return Result<int>.Failure("حسابِ ودیعه/اختلاف در تنظیماتِ گردشگری تعریف نشده.");

            var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
            var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
            if (lockMsg is not null) return Result<int>.Failure(lockMsg);

            await _uow.BeginTransactionAsync(ct);
            try
            {
                var amount = Math.Abs(diff);
                var number = await _vouchers.GetNextNumberAsync(companyId, ct);
                var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                    9, $"تعدیلِ آشتیِ ودیعه — گزارشِ {report.Id}", $"TUR-RECON-{number}");
                if (diff > 0)
                {
                    // تأمین‌کننده بیشتر کسر کرده → ودیعه بیشتر کم شود؛ اختلاف هزینه/زیان.
                    v.AddItem(VoucherItem.Create(0, 1, set.DepositDifferenceAccountId.Value, amount, 0, "اختلافِ آشتیِ ودیعه"));
                    v.AddItem(VoucherItem.Create(0, 2, set.SupplierDepositAccountId.Value, 0, amount, "تعدیلِ ودیعه"));
                }
                else
                {
                    // تأمین‌کننده کمتر کسر کرده → ودیعه افزایش؛ اختلاف درآمد/سود.
                    v.AddItem(VoucherItem.Create(0, 1, set.SupplierDepositAccountId.Value, amount, 0, "تعدیلِ ودیعه"));
                    v.AddItem(VoucherItem.Create(0, 2, set.DepositDifferenceAccountId.Value, 0, amount, "اختلافِ آشتیِ ودیعه"));
                }
                v.Post(_user.UserId ?? 0);
                await _vouchers.AddAsync(v, ct);
                await _uow.SaveChangesAsync(ct);
                adjVoucherId = v.Id;

                report.Reconcile(req.SupplierDeductedAmount, adjVoucherId, req.Note);
                _reports.Update(report);
                await _uow.SaveChangesAsync(ct);
                await _uow.CommitTransactionAsync(ct);
                return Result<int>.Success(report.Id);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync(ct);
                return Result<int>.Failure(ex.GetBaseException().Message);
            }
        }

        // بدونِ اختلاف — فقط علامتِ آشتی.
        report.Reconcile(req.SupplierDeductedAmount, null, req.Note);
        _reports.Update(report);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(report.Id);
    }
}
