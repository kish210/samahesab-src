using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Contracting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Contracting.Commands;

// ════════════════════════════════════════════════════════════════════════════
// CON-C1-3 — ثبتِ صورت‌وضعیت (محاسبهٔ آبشار) + Post با سندِ متوازن.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>ذخیرهٔ صورت‌وضعیت (Draft) با محاسبهٔ آبشار (موتورِ StatementWaterfallEngine).</summary>
public record SaveProgressStatementCommand(
    int ContractProjectId, int Number, StatementType Type, string Date,
    decimal CumulativeGrossWork, decimal PreviousCumulative,
    decimal AdjustmentAmount = 0, decimal MaterialDiffAmount = 0, decimal Penalty = 0, decimal Other = 0)
    : IRequest<Result<int>>;

public class SaveProgressStatementCommandHandler : IRequestHandler<SaveProgressStatementCommand, Result<int>>
{
    private readonly IRepository<ContractProject> _projects;
    private readonly IRepository<ContractingSetting> _settings;
    private readonly IRepository<AdvancePayment> _advances;
    private readonly IRepository<ProgressStatement> _statements;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveProgressStatementCommandHandler(IRepository<ContractProject> projects, IRepository<ContractingSetting> settings,
        IRepository<AdvancePayment> advances, IRepository<ProgressStatement> statements, IUnitOfWork uow, ICurrentUserService user)
    { _projects = projects; _settings = settings; _advances = advances; _statements = statements; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveProgressStatementCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var project = await _projects.FindSingleAsync(p => p.Id == req.ContractProjectId && p.CompanyId == companyId, ct);
        if (project is null) return Result<int>.Failure("پیمان یافت نشد.");

        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        var input = ContractingRates.BuildWaterfallInput(req, project, set,
            advanceOutstanding: await OutstandingAsync(companyId, project.Id, ct));
        var r = StatementWaterfallEngine.Compute(input);

        var st = ProgressStatement.Create(companyId, project.Id, req.Number, req.Type, req.Date,
            req.CumulativeGrossWork, req.PreviousCumulative, req.AdjustmentAmount, req.MaterialDiffAmount);
        st.SetComputed(r.PeriodWork, r.GrossThisPeriod, r.AdvanceRecovery, r.Retention, r.Insurance, r.Tax,
            r.Penalty, r.Other, r.NetPayable);
        await _statements.AddAsync(st, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(st.Id);
    }

    private async Task<decimal> OutstandingAsync(int companyId, int projectId, CancellationToken ct) =>
        (await _advances.FindAsync(a => a.CompanyId == companyId && a.ContractProjectId == projectId, ct))
        .Sum(a => a.Outstanding);
}

/// <summary>Approve→Post: سندِ متوازنِ صورت‌وضعیت + بازیافتِ پیش‌پرداخت + ردیف‌های کسرِ ممیزی.</summary>
public record PostProgressStatementCommand(int StatementId, int BranchId, int FiscalYearId) : IRequest<Result<int>>;

public class PostProgressStatementCommandHandler : IRequestHandler<PostProgressStatementCommand, Result<int>>
{
    private readonly IRepository<ProgressStatement> _statements;
    private readonly IRepository<ContractProject> _projects;
    private readonly IRepository<ContractingSetting> _settings;
    private readonly IRepository<AdvancePayment> _advances;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public PostProgressStatementCommandHandler(IRepository<ProgressStatement> statements, IRepository<ContractProject> projects,
        IRepository<ContractingSetting> settings, IRepository<AdvancePayment> advances, IVoucherRepository vouchers,
        IRepository<FiscalYear> fiscalYears, IUnitOfWork uow, ICurrentUserService user)
    {
        _statements = statements; _projects = projects; _settings = settings; _advances = advances;
        _vouchers = vouchers; _fiscalYears = fiscalYears; _uow = uow; _user = user;
    }

    public async Task<Result<int>> Handle(PostProgressStatementCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var st = await _statements.FindSingleAsync(s => s.Id == req.StatementId && s.CompanyId == companyId, ct);
        if (st is null) return Result<int>.Failure("صورت‌وضعیت یافت نشد.");
        if (st.Status == StatementStatus.Posted) return Result<int>.Failure("این صورت‌وضعیت قبلاً ثبت شده.");

        var project = await _projects.FindSingleAsync(p => p.Id == st.ContractProjectId, ct);
        if (project is null) return Result<int>.Failure("پیمان یافت نشد.");
        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        if (set?.ReceivableAccountId is null || set.RevenueAccountId is null || set.RetentionDepositAccountId is null
            || set.InsuranceDepositAccountId is null || set.PrepaidTaxAccountId is null
            || set.AdvanceLiabilityAccountId is null || set.PenaltyExpenseAccountId is null)
            return Result<int>.Failure("نگاشتِ حساب‌های پیمانکاری در تنظیمات کامل نیست.");

        // گاردِ مبلغ: سند نباید مبلغِ منفی/صفر داشته باشد (وگرنه VoucherItem.Create استثناءِ مبهم می‌دهد).
        if (st.GrossThisPeriod <= 0)
            return Result<int>.Failure("ناخالصِ این دوره صفر/منفی است؛ صورت‌وضعیت قابلِ ثبتِ سند نیست.");
        if (st.NetPayable < 0)
            return Result<int>.Failure("کسورات از ناخالص بیشتر است (خالصِ منفی)؛ درصدها/تعدیل را بازبینی کنید.");

        // گاردِ کهنگی: بازیافتِ ذخیره‌شده نباید از ماندهٔ فعلیِ پیش‌پرداخت بیشتر باشد (وگرنه سندِ بدهیِ
        // پیش‌پرداخت بیش از رکوردها بدهکار می‌شد — ناهماهنگیِ GL، اگر صورت‌وضعیتِ دیگری بینِ Save و Post پست شده باشد).
        if (st.AdvanceRecovery > 0)
        {
            var outstanding = (await _advances.FindAsync(
                a => a.CompanyId == companyId && a.ContractProjectId == st.ContractProjectId, ct)).Sum(a => a.Outstanding);
            if (st.AdvanceRecovery > outstanding + 0.01m)
                return Result<int>.Failure("بازیافتِ پیش‌پرداختِ ذخیره‌شده از ماندهٔ فعلی بیشتر است؛ صورت‌وضعیت را دوباره ذخیره (محاسبه) کنید.");
        }

        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, st.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, st.Date,
                9, $"صورت‌وضعیت {st.Number} — پیمانِ {project.Code}", $"CON-{number}");
            int row = 1;
            void Dr(int acc, decimal amt, string desc) { if (amt > 0) v.AddItem(VoucherItem.Create(0, row++, acc, amt, 0, desc)); }

            Dr(set.ReceivableAccountId.Value, st.NetPayable, "خالصِ دریافتنی از کارفرما");
            Dr(set.RetentionDepositAccountId.Value, st.Retention, "سپردهٔ حسن‌انجام‌کار");
            Dr(set.InsuranceDepositAccountId.Value, st.Insurance, "سپردهٔ بیمه");
            Dr(set.PrepaidTaxAccountId.Value, st.Tax, "پیش‌پرداختِ مالیات");
            Dr(set.AdvanceLiabilityAccountId.Value, st.AdvanceRecovery, "بازیافتِ پیش‌پرداخت");
            Dr(set.PenaltyExpenseAccountId.Value, st.Penalty + st.Other, "جریمه/سایر");
            // درآمدِ پیمان (بستانکار) با تگِ بُعدِ پروژه برای سود/زیان.
            v.AddItem(VoucherItem.Create(0, row++, set.RevenueAccountId.Value, 0, st.GrossThisPeriod,
                "درآمدِ پیمان", projectId: project.ProjectDimensionId));
            v.Post(_user.UserId ?? 0);
            await _vouchers.AddAsync(v, ct);
            await _uow.SaveChangesAsync(ct);

            // بازیافتِ پیش‌پرداخت روی رکوردهای پیش‌پرداخت (به‌ترتیب، سقف‌دار).
            if (st.AdvanceRecovery > 0)
            {
                var remaining = st.AdvanceRecovery;
                var advances = (await _advances.FindAsync(a => a.CompanyId == companyId && a.ContractProjectId == project.Id, ct))
                    .OrderBy(a => a.Id);
                foreach (var adv in advances)
                {
                    if (remaining <= 0) break;
                    remaining -= adv.Recover(remaining);
                    _advances.Update(adv);
                }
            }

            // ردیف‌های کسرِ ممیزی (Type/Base/Rate/Amount/AccountId).
            st.ClearDeductions();
            AddDed(st, DeductionType.AdvanceRecovery, st.PeriodWork, st.AdvanceRecovery, set.AdvanceLiabilityAccountId.Value);
            AddDed(st, DeductionType.Retention, st.GrossThisPeriod, st.Retention, set.RetentionDepositAccountId.Value);
            AddDed(st, DeductionType.Insurance, st.GrossThisPeriod, st.Insurance, set.InsuranceDepositAccountId.Value);
            AddDed(st, DeductionType.Tax, st.GrossThisPeriod, st.Tax, set.PrepaidTaxAccountId.Value);
            AddDed(st, DeductionType.Penalty, 0, st.Penalty, set.PenaltyExpenseAccountId.Value);
            AddDed(st, DeductionType.Other, 0, st.Other, set.PenaltyExpenseAccountId.Value);

            st.MarkPosted(v.Id);
            _statements.Update(st);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(st.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }

    private static void AddDed(ProgressStatement st, DeductionType type, decimal @base, decimal amount, int accountId)
    {
        if (amount > 0) st.AddDeduction(StatementDeduction.Create(type, @base, 0, amount, accountId));
    }
}

/// <summary>حلِ نرخ‌ها (پروژه > پیش‌فرضِ تنظیمات) و ساختِ ورودیِ آبشار.</summary>
internal static class ContractingRates
{
    public static WaterfallInput BuildWaterfallInput(SaveProgressStatementCommand req, ContractProject p,
        ContractingSetting? set, decimal advanceOutstanding) => new(
        CumulativeGrossWork: req.CumulativeGrossWork,
        PreviousCumulative: req.PreviousCumulative,
        AdjustmentAmount: p.AdjustmentEnabled ? req.AdjustmentAmount : 0,
        MaterialDiffAmount: req.MaterialDiffAmount,
        AdvancePercent: Pick(p.AdvancePercent, set?.DefaultAdvancePercent),
        RetentionPercent: Pick(p.RetentionPercent, set?.DefaultRetentionPercent),
        InsurancePercent: Pick(p.InsuranceWithholdPercent, set?.DefaultInsuranceWithholdPercent),
        TaxPercent: Pick(p.TaxWithholdPercent, set?.DefaultTaxWithholdPercent),
        Penalty: req.Penalty, Other: req.Other,
        AdvanceOutstanding: advanceOutstanding);

    private static decimal Pick(decimal projectPercent, decimal? defaultPercent)
        => projectPercent > 0 ? projectPercent : (defaultPercent ?? 0);
}
