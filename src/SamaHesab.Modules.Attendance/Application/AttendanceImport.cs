using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>یک ردیفِ ایمپورتِ تردد (از فایلِ دستگاه/اکسل، CSV).</summary>
public record AttendanceImportRow(string Code, string Date, TimeOnly? CheckIn, TimeOnly? CheckOut,
    string Status, string? Error = null)
{
    public bool IsValid => Error is null;
}

/// <summary>
/// ATT-C1-4 — تجزیه‌گرِ خالصِ فایلِ ترددِ دستگاه/اکسل (CSV).
/// ستون‌ها: «کد, تاریخ, ورود, خروج, وضعیت» — ورود/خروج «HH:mm» (اختیاری)، وضعیت پیش‌فرض «حاضر».
/// ارقامِ فارسی/عربی نرمال می‌شوند؛ خطِ سرستون و ردیف‌های ناقص رد می‌شوند.
/// </summary>
public static class AttendanceImportParser
{
    public static List<AttendanceImportRow> Parse(string csv)
    {
        var rows = new List<AttendanceImportRow>();
        if (string.IsNullOrWhiteSpace(csv)) return rows;

        foreach (var raw in csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var cols = line.Split(new[] { ',', '\t', ';' });
            var code = NormalizeDigits(cols[0].Trim());
            if (code.Length == 0) continue;
            // خطِ سرستون (کد/code) را رد کن.
            if (code is "کد" or "Code" or "code") continue;
            if (cols.Length < 2) { rows.Add(Bad(code, "تاریخ ندارد")); continue; }

            var date = NormalizeDigits(cols[1].Trim());
            if (date.Length < 6) { rows.Add(Bad(code, "تاریخِ نامعتبر")); continue; }

            var inT = ParseTime(cols.ElementAtOrDefault(2));
            var outT = ParseTime(cols.ElementAtOrDefault(3));
            var status = (cols.ElementAtOrDefault(4) ?? "").Trim();
            if (status.Length == 0) status = (inT.HasValue || outT.HasValue) ? "حاضر" : "حاضر";

            rows.Add(new AttendanceImportRow(code, date, inT, outT, status));
        }
        return rows;
    }

    private static AttendanceImportRow Bad(string code, string err) =>
        new(code, "", null, null, "حاضر", err);

    private static TimeOnly? ParseTime(string? s)
    {
        s = NormalizeDigits((s ?? "").Trim());
        if (s.Length == 0) return null;
        return TimeOnly.TryParse(s, out var t) ? t : null;
    }

    internal static string NormalizeDigits(string input)
    {
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));
            else if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
        }
        return new string(chars);
    }
}

/// <summary>ATT-C1-4 — ایمپورتِ ترددِ یک فایل (CSV) و درجِ idempotent در DB.</summary>
public record ImportAttendanceCommand(string Csv) : IRequest<Result<AttendanceImportResult>>;

public record AttendanceImportResult(int Imported, int Skipped, IReadOnlyList<string> Errors);

public class ImportAttendanceCommandHandler : IRequestHandler<ImportAttendanceCommand, Result<AttendanceImportResult>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public ImportAttendanceCommandHandler(IRepository<Employee> employees, IRepository<AttendanceRecord> records,
        IUnitOfWork uow, ICurrentUserService user)
    { _employees = employees; _records = records; _uow = uow; _user = user; }

    public async Task<Result<AttendanceImportResult>> Handle(ImportAttendanceCommand req, CancellationToken ct)
    {
        var parsed = AttendanceImportParser.Parse(req.Csv);
        if (parsed.Count == 0) return Result<AttendanceImportResult>.Failure("فایلِ تردد خالی یا نامعتبر است.");

        var companyId = _user.CompanyId ?? 1;
        var empByCode = (await _employees.FindAsync(e => e.CompanyId == companyId, ct))
            .GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First());

        int imported = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var row in parsed)
        {
            if (!row.IsValid) { skipped++; errors.Add($"{row.Code}: {row.Error}"); continue; }
            if (!empByCode.TryGetValue(row.Code, out var emp))
            { skipped++; errors.Add($"{row.Code}: کارمند یافت نشد"); continue; }

            var rec = await _records.FindSingleAsync(a => a.EmployeeId == emp.Id && a.WorkDate == row.Date, ct);
            var isNew = rec is null;
            rec ??= AttendanceRecord.Create(emp.Id, row.Date, row.Status);
            AttendanceCommandHelpers.ApplyStatus(rec, row.Status, null, null, row.CheckIn, row.CheckOut);
            if (isNew) await _records.AddAsync(rec, ct);
            imported++;
        }

        await _uow.SaveChangesAsync(ct);
        return Result<AttendanceImportResult>.Success(
            new AttendanceImportResult(imported, skipped, errors));
    }
}
