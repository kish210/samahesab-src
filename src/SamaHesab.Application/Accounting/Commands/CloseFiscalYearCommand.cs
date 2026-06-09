using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// بستن سال مالی: ۱) سند اختتامیه — صفرکردن حساب‌های درآمد/هزینه و انتقال سود/زیان به «سود (زیان) جاری».
/// ۲) (اختیاری) سند افتتاحیه سال بعد — انتقال مانده‌ی حساب‌های دائمی (دارایی/بدهی/سرمایه).
/// هر دو سند به‌طور سازه‌ای متوازن‌اند و قطعی می‌شوند.
/// </summary>
public record CloseFiscalYearCommand(
    int FiscalYearId,
    int BranchId,
    string FromDate,
    string ToDate,
    string ClosingDate,
    bool GenerateOpening = false,
    int NextFiscalYearId = 0,
    string? OpeningDate = null
) : IRequest<Result<CloseFiscalYearResult>>;

public record CloseFiscalYearResult(int ClosingVoucherId, int? OpeningVoucherId,
    decimal Revenue, decimal Expense, decimal NetProfit);

public class CloseFiscalYearCommandHandler : IRequestHandler<CloseFiscalYearCommand, Result<CloseFiscalYearResult>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    private const string ProfitLossCode = "5-04";  // سود (زیان) جاری

    public CloseFiscalYearCommandHandler(IVoucherRepository vouchers, IAccountRepository accounts,
        IUnitOfWork uow, ICurrentUserService currentUser)
    { _vouchers = vouchers; _accounts = accounts; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<CloseFiscalYearResult>> Handle(CloseFiscalYearCommand req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var userId = _currentUser.UserId ?? 0;

        var plAccount = await _accounts.GetByCodeAsync(companyId, ProfitLossCode, ct);
        if (plAccount is null)
            return Result<CloseFiscalYearResult>.Failure($"حساب «سود (زیان) جاری» (کد {ProfitLossCode}) یافت نشد.");

        var accountsById = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => a);

        // همه‌ی اسناد بازه به‌جز اسناد برگشت‌خورده (هماهنگ با گزارش‌های تراز/معین که همه را لحاظ می‌کنند).
        var vouchers = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => !v.IsReversed)
            .ToList();

        // مانده‌ی هر حساب: بدهکار منهای بستانکار
        var net = new Dictionary<int, decimal>();
        foreach (var i in vouchers.SelectMany(v => v.Items))
            net[i.AccountId] = net.GetValueOrDefault(i.AccountId) + (i.Debit - i.Credit);

        string TypeOf(int accId) => accountsById.TryGetValue(accId, out var a) ? (a.AccountType ?? "") : "";

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ── ۱) سند اختتامیه: صفرکردن درآمد/هزینه ──
            var closeNumber = await _vouchers.GetNextNumberAsync(companyId, ct);
            var closing = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, closeNumber,
                req.ClosingDate, 2 /*اختتامیه (CLOSE)*/, "سند اختتامیه — بستن حساب‌های سود و زیان");

            decimal revenue = 0, expense = 0; int row = 1;
            foreach (var (accId, n) in net)
            {
                if (n == 0) continue;
                var type = TypeOf(accId);
                if (type != "درآمد" && type != "هزینه") continue;
                if (type == "درآمد") revenue += -n; else expense += n;   // درآمد مانده بستانکار، هزینه مانده بدهکار
                // ردیف صفرکننده: عکسِ مانده
                closing.AddItem(VoucherItem.Create(0, row++, accId,
                    debit: n < 0 ? -n : 0, credit: n > 0 ? n : 0, description: "بستن حساب"));
            }

            if (closing.Items.Count == 0)
                { await _uow.RollbackTransactionAsync(ct); return Result<CloseFiscalYearResult>.Failure("حساب درآمد/هزینه‌ای برای بستن یافت نشد."); }

            // ردیف توازن به «سود (زیان) جاری»
            var closingNet = closing.Items.Sum(i => i.Debit) - closing.Items.Sum(i => i.Credit);
            closing.AddItem(VoucherItem.Create(0, row++, plAccount.Id,
                debit: closingNet < 0 ? -closingNet : 0, credit: closingNet > 0 ? closingNet : 0,
                description: "انتقال سود (زیان) دوره"));

            closing.Post(userId);
            await _vouchers.AddAsync(closing, ct);
            await _uow.SaveChangesAsync(ct);

            var netProfit = revenue - expense;

            // ── ۲) سند افتتاحیه سال بعد (اختیاری): انتقال مانده‌های دائمی ──
            int? openingId = null;
            if (req.GenerateOpening)
            {
                var openNumber = await _vouchers.GetNextNumberAsync(companyId, ct);
                var opening = Voucher.Create(companyId, req.BranchId,
                    req.NextFiscalYearId > 0 ? req.NextFiscalYearId : req.FiscalYearId,
                    openNumber, req.OpeningDate ?? req.ClosingDate, 1 /*افتتاحیه (OPEN)*/,
                    "سند افتتاحیه — انتقال مانده‌های دائمی به سال بعد");

                int orow = 1;
                foreach (var (accId, n) in net)
                {
                    if (n == 0) continue;
                    var type = TypeOf(accId);
                    if (type != "دارایی" && type != "بدهی" && type != "سرمایه") continue;
                    // مانده با همان سمت منتقل می‌شود (دارایی بدهکار، بدهی/سرمایه بستانکار)
                    opening.AddItem(VoucherItem.Create(0, orow++, accId,
                        debit: n > 0 ? n : 0, credit: n < 0 ? -n : 0, description: "انتقال مانده"));
                }

                // توازن: سود انباشته‌ی دوره به «سود (زیان) جاری»
                var openNet = opening.Items.Sum(i => i.Debit) - opening.Items.Sum(i => i.Credit);
                if (openNet != 0)
                    opening.AddItem(VoucherItem.Create(0, orow++, plAccount.Id,
                        debit: openNet < 0 ? -openNet : 0, credit: openNet > 0 ? openNet : 0,
                        description: "سود (زیان) انباشته"));

                if (opening.Items.Count > 0)
                {
                    opening.Post(userId);
                    await _vouchers.AddAsync(opening, ct);
                    await _uow.SaveChangesAsync(ct);
                    openingId = opening.Id;
                }
            }

            await _uow.CommitTransactionAsync(ct);
            return Result<CloseFiscalYearResult>.Success(
                new CloseFiscalYearResult(closing.Id, openingId, revenue, expense, netProfit));
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<CloseFiscalYearResult>.Failure(ex.GetBaseException().Message);
        }
    }
}
