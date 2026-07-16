using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM;

/// <summary>
/// U-PARTY-LEDGER (backlog #9) — نقطهٔ ورودیِ یکتا برایِ هر تغییرِ Party.Balance: هم مانده را
/// به‌روز می‌کند هم یک ردیفِ امضادار در دفترِ معین (<see cref="PartyLedgerEntry"/>) ثبت می‌کند.
/// جایگزینِ فراخوانِ پراکندهٔ <c>party.UpdateBalance(party.Balance + delta)</c> در همهٔ نقاطِ
/// Core/ماژول‌ها (هدف: غیرممکن‌شدنِ «جا موندنِ» به‌روزرسانیِ مانده در یک نقطهٔ نو).
/// <c>public</c> است (نه internal) چون ماژول‌هایی مثلِ Tourism در اسمبلیِ جدا هم باید صدایش بزنند.
/// </summary>
public static class PartyLedger
{
    public static async Task RecordAsync(IRepository<PartyLedgerEntry> ledger, Party party,
        decimal delta, string date, string docType, string? docNumber, string? description,
        CancellationToken ct)
    {
        if (delta == 0) return;
        party.UpdateBalance(party.Balance + delta);
        await ledger.AddAsync(
            PartyLedgerEntry.Create(party.CompanyId, party.Id, date, docType, docNumber, description, delta), ct);
    }
}
