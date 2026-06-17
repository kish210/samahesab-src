using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Import;

// فاز ۱۲ G4 — مهاجرتِ دادهٔ مشتری/تأمین‌کننده/کالا از اکسل (ردیف‌های خام = دیکشنریِ سرستون→مقدار).
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

    /// <summary>پارسِ عدد با تحملِ رقمِ فارسی/عربی، جداکنندهٔ هزار و فاصله.</summary>
    public static decimal Dec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.Trim())
        {
            if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));        // فارسی
            else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));   // عربی
            else if (char.IsDigit(ch) || ch == '.' || ch == '-') sb.Append(ch);
            // کاما/فاصله/سایر نویسه‌ها حذف
        }
        return decimal.TryParse(sb.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
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
                    RowMap.Get(row, "کد پستی", "کدپستی", "PostalCode"));

                // RC-7b — اعتبارسنجیِ هویتِ مالیاتی: نامعتبر را ذخیره نکن و هشدار بده (مشتری حفظ می‌شود).
                var national = RowMap.Get(row, "کد ملی", "شناسه ملی", "کد/شناسه ملی", "NationalCode");
                var economic = RowMap.Get(row, "کد اقتصادی", "EconomicCode");
                var nationalOk = type == "حقوقی"
                    ? SamaHesab.Application.Common.Validation.IranianIdentity.IsValidEconomicId(national)
                    : SamaHesab.Application.Common.Validation.IranianIdentity.IsValidNationalCode(national);
                if (!nationalOk) { if (errors.Count < 10) errors.Add($"ردیف {line}: کد/شناسهٔ ملیِ «{national}» نامعتبر بود و نادیده گرفته شد."); national = null; }
                if (!SamaHesab.Application.Common.Validation.IranianIdentity.IsValidEconomicId(economic))
                { if (errors.Count < 10) errors.Add($"ردیف {line}: کدِ اقتصادیِ «{economic}» نامعتبر بود و نادیده گرفته شد."); economic = null; }

                c.SetDetails(national, economic, null, RowMap.Get(row, "توضیحات", "Notes"));

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

// ─── اشخاص (فایلِ ترکیبیِ مشتری+تأمین‌کننده — مثلِ حساب‌فا) ───
// یک فایلِ «اشخاص» با ستون‌های پرچمِ «مشتری»/«تأمین‌کننده» را بر اساسِ پرچم تفکیک و به
// کامندهای موجودِ مشتری/تأمین‌کننده می‌سپارد (استفادهٔ مجددِ کاملِ منطق/اعتبارسنجی).
// ردیفی که هر دو پرچم را دارد، هم مشتری و هم تأمین‌کننده ساخته می‌شود. اگر هیچ پرچمی نبود → مشتری.
public record ImportPersonsCommand(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows) : IRequest<ImportResult>;

public class ImportPersonsCommandHandler : IRequestHandler<ImportPersonsCommand, ImportResult>
{
    private readonly IMediator _mediator;
    public ImportPersonsCommandHandler(IMediator mediator) { _mediator = mediator; }

    private static bool Flag(IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        var v = RowMap.Get(row, keys);
        if (v == null) return false;
        v = v.Trim();
        return v is "+" or "بله" or "1" or "true" or "True" or "✓" or "yes" or "Yes";
    }

    public async Task<ImportResult> Handle(ImportPersonsCommand req, CancellationToken ct)
    {
        var customerRows = new List<IReadOnlyDictionary<string, string>>();
        var supplierRows = new List<IReadOnlyDictionary<string, string>>();

        foreach (var row in req.Rows)
        {
            var isCust = Flag(row, "مشتری", "Customer", "Is Customer");
            var isSupp = Flag(row, "تأمین‌کننده", "تامین کننده", "تامین‌کننده", "Supplier", "Vendor");
            if (!isCust && !isSupp) isCust = true;   // بدونِ پرچم → مشتری
            if (isCust) customerRows.Add(row);
            if (isSupp) supplierRows.Add(row);
        }

        var errors = new List<string>();
        int imp = 0, skip = 0, fail = 0;

        if (customerRows.Count > 0)
        {
            var r = await _mediator.Send(new ImportCustomersCommand(customerRows), ct);
            imp += r.Imported; skip += r.Skipped; fail += r.Failed;
            errors.AddRange(r.Errors.Select(e => "مشتری » " + e));
        }
        if (supplierRows.Count > 0)
        {
            var r = await _mediator.Send(new ImportSuppliersCommand(supplierRows), ct);
            imp += r.Imported; skip += r.Skipped; fail += r.Failed;
            errors.AddRange(r.Errors.Select(e => "تأمین‌کننده » " + e));
        }

        return new ImportResult(imp, skip, fail, errors);
    }
}

// ──────────────────────────────── کالا (G4.2) ────────────────────────────────
public record ImportProductsCommand(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows) : IRequest<ImportResult>;

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, ImportResult>
{
    private readonly IRepository<Product> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IUnitLookup _units;
    public ImportProductsCommandHandler(IRepository<Product> repo, IUnitOfWork uow, ICurrentUserService user, IUnitLookup units)
    { _repo = repo; _uow = uow; _user = user; _units = units; }

    public async Task<ImportResult> Handle(ImportProductsCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var defaultUnit = _units.DefaultUnitId();
        var existing = (await _repo.FindAsync(p => p.CompanyId == companyId, ct))
            .Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int imported = 0, skipped = 0, failed = 0, seq = existing.Count, line = 1;
        var errors = new List<string>();

        foreach (var row in req.Rows)
        {
            line++;
            try
            {
                var name = RowMap.Get(row, "نام", "نام کالا", "شرح", "Name");
                if (name == null) { failed++; if (errors.Count < 10) errors.Add($"ردیف {line}: نامِ کالا خالی است."); continue; }

                var unitId = _units.Resolve(RowMap.Get(row, "واحد", "واحد اصلی", "Unit")) ?? defaultUnit;
                if (unitId is null) { failed++; if (errors.Count < 10) errors.Add($"ردیف {line}: واحدی در سیستم تعریف نشده."); continue; }

                var code = RowMap.Get(row, "کد", "کد کالا", "Code");
                if (code == null) { do { code = $"P{++seq}"; } while (existing.Contains(code)); }
                if (existing.Contains(code)) { skipped++; continue; }

                var sale = RowMap.Dec(RowMap.Get(row, "قیمت فروش", "فروش", "SalePrice", "قیمت"));
                var purchase = RowMap.Dec(RowMap.Get(row, "قیمت خرید", "خرید", "PurchasePrice"));
                var wholesale = RowMap.Dec(RowMap.Get(row, "قیمت عمده", "عمده", "WholesalePrice"));
                var consumer = RowMap.Dec(RowMap.Get(row, "قیمت مصرف‌کننده", "مصرف‌کننده", "ConsumerPrice"));
                var tax = RowMap.Dec(RowMap.Get(row, "مالیات", "مالیات فروش", "نرخ مالیات", "TaxRate"));
                var barcode = RowMap.Get(row, "بارکد", "Barcode");

                var p = Product.Create(companyId, code, name, unitId.Value, sale, purchase);
                if (wholesale > 0 || consumer > 0 || tax > 0) p.UpdatePrices(purchase, sale, wholesale, consumer, tax);
                if (!string.IsNullOrWhiteSpace(barcode)) p.UpdateDetails(name, null, null, null, barcode, null, null);

                await _repo.AddAsync(p, ct);
                existing.Add(code); imported++;
            }
            catch (Exception ex) { failed++; if (errors.Count < 10) errors.Add($"ردیف {line}: {ex.GetBaseException().Message}"); }
        }

        if (imported > 0) await _uow.SaveChangesAsync(ct);
        return new ImportResult(imported, skipped, failed, errors);
    }
}
