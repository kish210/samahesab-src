using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Import;

// فاز ۱۲ G4 — مهاجرتِ دادهٔ مشتری/تأمین‌کننده از اکسل (ردیف‌های خام = دیکشنریِ سرستون→مقدار).
public record ImportResult(int Imported, int Skipped, int Failed, IReadOnlyList<string> Errors);

// ── کمکیِ نگاشتِ ستون (نامِ سرستونِ فارسی → مقدار؛ چند مترادف) ──
internal static class RowMap
{
    public static string? Get(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var k in keys)
            if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
        return null;
    }
}

// ───────────────────────────── مشتری ─────────────────────────────
public record ImportCustomersCommand(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows) : IRequest<ImportResult>;

public class ImportCustomersCommandHandler : IRequestHandler<ImportCustomersCommand, ImportResult>
{
    private readonly IRepository<Customer> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public ImportCustomersCommandHandler(IRepository<Customer> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<ImportResult> Handle(ImportCustomersCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var existing = (await _repo.FindAsync(c => c.CompanyId == companyId, ct))
            .Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int imported = 0, skipped = 0, failed = 0, seq = existing.Count, line = 1;
        var errors = new List<string>();

        foreach (var row in req.Rows)
        {
            line++;
            try
            {
                var first = RowMap.Get(row, "نام", "نام مشتری", "FirstName", "Name");
                var last = RowMap.Get(row, "نام خانوادگی", "نام‌خانوادگی", "LastName", "Family");
                var company = RowMap.Get(row, "نام شرکت", "شرکت", "CompanyName");
                var type = company != null && first == null && last == null ? "حقوقی" : "حقیقی";

                var code = RowMap.Get(row, "کد", "کد مشتری", "Code");
                if (code == null) { do { code = $"C{++seq}"; } while (existing.Contains(code)); }
                if (existing.Contains(code)) { skipped++; continue; }

                var c = Customer.Create(companyId, code, type, first, last, company);
                c.UpdateContactInfo(
                    RowMap.Get(row, "تلفن", "Phone"), RowMap.Get(row, "موبایل", "تلفن همراه", "همراه", "Mobile"),
                    RowMap.Get(row, "ایمیل", "Email"), RowMap.Get(row, "استان", "Province"),
                    RowMap.Get(row, "شهر", "City"), RowMap.Get(row, "آدرس", "نشانی", "Address"),
                    RowMap.Get(row, "کد پستی", "PostalCode"));
                c.SetDetails(RowMap.Get(row, "کد ملی", "شناسه ملی", "کد/شناسه ملی", "NationalCode"),
                    RowMap.Get(row, "کد اقتصادی", "EconomicCode"), null, RowMap.Get(row, "توضیحات", "Notes"));

                await _repo.AddAsync(c, ct);
                existing.Add(code); imported++;
            }
            catch (Exception ex) { failed++; if (errors.Count < 10) errors.Add($"ردیف {line}: {ex.GetBaseException().Message}"); }
        }

        if (imported > 0) await _uow.SaveChangesAsync(ct);
        return new ImportResult(imported, skipped, failed, errors);
    }
}

// ──────────────────────────── تأمین‌کننده ────────────────────────────
public record ImportSuppliersCommand(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows) : IRequest<ImportResult>;

public class ImportSuppliersCommandHandler : IRequestHandler<ImportSuppliersCommand, ImportResult>
{
    private readonly IRepository<Supplier> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public ImportSuppliersCommandHandler(IRepository<Supplier> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<ImportResult> Handle(ImportSuppliersCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var existing = (await _repo.FindAsync(s => s.CompanyId == companyId, ct))
            .Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int imported = 0, skipped = 0, failed = 0, seq = existing.Count, line = 1;
        var errors = new List<string>();

        foreach (var row in req.Rows)
        {
            line++;
            try
            {
                var first = RowMap.Get(row, "نام", "نام تأمین‌کننده", "FirstName", "Name");
                var last = RowMap.Get(row, "نام خانوادگی", "نام‌خانوادگی", "LastName");
                var company = RowMap.Get(row, "نام شرکت", "شرکت", "CompanyName");
                var type = company != null && first == null && last == null ? "حقوقی" : "حقیقی";

                var code = RowMap.Get(row, "کد", "کد تأمین‌کننده", "Code");
                if (code == null) { do { code = $"S{++seq}"; } while (existing.Contains(code)); }
                if (existing.Contains(code)) { skipped++; continue; }

                var s = Supplier.Create(companyId, code, type, first, last, company);
                s.UpdateContactInfo(
                    RowMap.Get(row, "تلفن", "Phone"), RowMap.Get(row, "موبایل", "تلفن همراه", "همراه", "Mobile"),
                    RowMap.Get(row, "ایمیل", "Email"), RowMap.Get(row, "استان", "Province"),
                    RowMap.Get(row, "شهر", "City"), RowMap.Get(row, "آدرس", "نشانی", "Address"));

                await _repo.AddAsync(s, ct);
                existing.Add(code); imported++;
            }
            catch (Exception ex) { failed++; if (errors.Count < 10) errors.Add($"ردیف {line}: {ex.GetBaseException().Message}"); }
        }

        if (imported > 0) await _uow.SaveChangesAsync(ct);
        return new ImportResult(imported, skipped, failed, errors);
    }
}
