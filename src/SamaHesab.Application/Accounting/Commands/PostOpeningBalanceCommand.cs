using MediatR;
using SamaHesab.Application.Common.Models;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// فاز ۱۲ P-G6 — ثبتِ ماندهٔ افتتاحیهٔ مشتریِ جدید به‌صورتِ سندِ افتتاحیه (نوعِ سند OPEN).
/// API برنامه‌ای برای راه‌اندازی/مهاجرت؛ هستهٔ کار به `CreateVoucherCommand` واگذار می‌شود
/// (همان قفلِ دوره/توازن/شماره‌گذاری). صفحهٔ «ثبت سند» هم با انتخابِ نوعِ «افتتاحیه» همین را می‌سازد.
/// </summary>
public record OpeningBalanceLine(int AccountId, decimal Debit, decimal Credit, string? Description = null);

public record PostOpeningBalanceCommand(
    int BranchId, int FiscalYearId, string Date, List<OpeningBalanceLine> Lines) : IRequest<Result<int>>;

public class PostOpeningBalanceCommandHandler : IRequestHandler<PostOpeningBalanceCommand, Result<int>>
{
    /// <summary>Acc.VoucherTypes با کدِ 'OPEN' (افتتاحیه) — مطابقِ seed (06_SeedData).</summary>
    public const int OpeningVoucherTypeId = 1;

    private readonly IMediator _mediator;
    public PostOpeningBalanceCommandHandler(IMediator mediator) => _mediator = mediator;

    public async Task<Result<int>> Handle(PostOpeningBalanceCommand req, CancellationToken ct)
    {
        if (req.Lines is null || req.Lines.Count == 0)
            return Result<int>.Failure("سندِ افتتاحیه باید حداقل یک ردیف داشته باشد.");

        var debit = req.Lines.Sum(l => l.Debit);
        var credit = req.Lines.Sum(l => l.Credit);
        if (System.Math.Abs(debit - credit) >= 0.01m)
            return Result<int>.Failure($"سندِ افتتاحیه تراز نیست (بدهکار {debit:N0} ≠ بستانکار {credit:N0}).");

        var items = req.Lines.Select((l, i) =>
            new VoucherItemDto(i + 1, l.AccountId, l.Debit, l.Credit,
                l.Description ?? "ماندهٔ افتتاحیه", null, null)).ToList();

        return await _mediator.Send(new CreateVoucherCommand(
            req.BranchId, req.FiscalYearId, req.Date, OpeningVoucherTypeId,
            "ماندهٔ افتتاحیه", "افتتاحیه", null, 1m, items), ct);
    }
}
