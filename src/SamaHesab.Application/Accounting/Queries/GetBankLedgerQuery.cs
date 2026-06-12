using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// دفتر بانک (R4 / #۱۹): ردیف‌های سند ثبت‌شده روی حساب کلِ یک حساب بانکی در یک بازهٔ تاریخ —
/// ورودیِ موتور مغایرت‌گیری <see cref="BankReconciliation"/>.
/// مبلغ هر ردیف به‌صورت خالص (بدهکار منهای بستانکار) است: واریز مثبت، برداشت منفی.
/// </summary>
public record GetBankLedgerQuery(int BankAccountId, string FromDate, string ToDate)
    : IRequest<BankLedgerResult>;

public record BankLedgerLineDto(int VoucherItemId, string Date, decimal Amount, string Description);
public record BankLedgerResult(string BankName, int GlAccountId, List<BankLedgerLineDto> Lines);

public class GetBankLedgerQueryHandler : IRequestHandler<GetBankLedgerQuery, BankLedgerResult>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<BankAccount> _bankAccounts;
    private readonly ICurrentUserService _user;

    public GetBankLedgerQueryHandler(IVoucherRepository vouchers,
        IRepository<BankAccount> bankAccounts, ICurrentUserService user)
    { _vouchers = vouchers; _bankAccounts = bankAccounts; _user = user; }

    public async Task<BankLedgerResult> Handle(GetBankLedgerQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        var bank = await _bankAccounts.GetByIdAsync(req.BankAccountId);
        if (bank is null) return new BankLedgerResult("", 0, new());

        var vouchers = await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct);

        var lines = vouchers
            .Where(v => !v.IsReversed)
            .SelectMany(v => v.Items
                .Where(i => i.AccountId == bank.AccountId)
                .Select(i => new BankLedgerLineDto(i.Id, v.VoucherDate, i.Debit - i.Credit,
                    string.IsNullOrWhiteSpace(i.Description) ? v.Description ?? "" : i.Description!)))
            .OrderBy(l => l.Date)
            .ToList();

        return new BankLedgerResult(bank.BankName, bank.AccountId, lines);
    }
}
