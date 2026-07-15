using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SamaHesab.Infrastructure.Data;

/// <summary>
/// اجراکنندهٔ مهاجرت‌های پایگاه‌داده در زمانِ استارت‌آپ (startup migration-runner).
/// مشکلِ ریشه‌ایِ تکرارشونده را حل می‌کند: «DBِ کاربر از مهاجرت‌ها عقب می‌ماند» →
/// «Invalid column/object name» → کرشِ صفحات.
///
/// قرارداد:
///   • فقط اسکریپت‌های **مهاجرتِ افزایشی** با پیشوندِ عددیِ ≥ <see cref="MinMigrationNumber"/>
///     اجرا می‌شوند (11،12،…،25،۲۶،…). همگی باید **idempotent** باشند (IF NOT EXISTS / COL_LENGTH …).
///   • اسکریپت‌های پایه/seed/دمو (01..09 — ساختِ اولیه، voucher-types، نمودار حساب) اجرا **نمی‌شوند**
///     (توسطِ نصاب روی DBِ تازه اجرا می‌شوند؛ دوباره‌اجرا = داده‌ی تکراری).
///   • اسکریپت‌های اعمال‌شده در جدولِ <c>dbo.__AppliedScripts</c> ثبت می‌شوند تا هر بار اجرا نشوند.
///
/// فایل‌های `database/*.sql` به‌صورتِ EmbeddedResource در همین اسمبلی بسته‌بندی شده‌اند
/// (csproj)، پس بعد از نصب هم بدونِ وابستگی به مسیرِ فایل کار می‌کند.
/// </summary>
public static class DatabaseMigrator
{
    private const int MinMigrationNumber = 11;
    private const string ResourceMarker = ".database.";

    /// <summary>schemaهای پایه (مطابقِ 01_CreateDatabase) — برنامه‌ای ساخته می‌شوند چون اسکریپتِ ۰۱
    /// مسیرهای لینوکسی/نامِ ثابت دارد و DB را drop می‌کند (روی ویندوز قابل‌اجرا نیست).</summary>
    private static readonly string[] BaseSchemas = { "Acc", "Inv", "Sal", "Pur", "Pos", "Crm", "Hrm", "Cfg", "Sec" };

    public static async Task RunAsync(string connectionString, Action<string>? log = null, CancellationToken ct = default)
    {
        var asm = typeof(DatabaseMigrator).Assembly;

        // ۰) اطمینان از وجودِ خودِ پایگاه‌داده (نصبِ تازه: DB هنوز ساخته نشده).
        await EnsureDatabaseExistsAsync(connectionString, log, ct);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await EnsureSchemasAsync(conn, log, ct);
        await EnsureTrackingTableAsync(conn, ct);
        var applied = await GetAppliedAsync(conn, ct);

        // ۱) اسکیمای پایه (02..09): اجرا اگر DB تازه است **یا داده‌های پایه ناقص‌اند** (بوت‌استرپِ ناقصِ قبلی).
        //    قبلاً فقط به نبودِ `Sec.Users` گِیت می‌شد؛ اگر جدول‌ها ساخته می‌شد ولی seed/نمودار حساب شکست می‌خورد،
        //    دفعهٔ بعد چون Sec.Users بود، داده‌های پایه هرگز ساخته نمی‌شد (= «فرمِ اطلاعاتِ پایه کار نمی‌کند»).
        //    `__AppliedScripts` از دوباره‌اجرای اسکریپت‌های *موفق* جلوگیری می‌کند؛ فقط شکست‌خورده‌ها دوباره تلاش می‌شوند.
        //    گِیت روی «نبودِ داده» است تا DBهای کامل (موجود/legacy با شرکت+حساب) دست‌نخورده بمانند.
        var needBase = await ObjectMissingAsync(conn, "Sec.Users", ct)
                    || await TableEmptyAsync(conn, "Cfg.Companies", ct)
                    || await TableEmptyAsync(conn, "Acc.Accounts", ct);
        if (needBase)
        {
            log?.Invoke("اجرای اسکریپت‌های پایه (ساختِ جدول‌ها/seed/نمودار حساب)...");
            foreach (var res in OrderedScripts(asm, IsBaseResource))
                await ApplyScriptAsync(conn, asm, res, applied, log, ct);

            if (await TableEmptyAsync(conn, "Cfg.Companies", ct) || await TableEmptyAsync(conn, "Acc.Accounts", ct))
                log?.Invoke("⚠ هشدار: پس از اجرا، داده‌های پایه (شرکت/نمودار حساب) همچنان ناقص است — خطاهای اسکریپتِ بالا را بررسی کنید.");
        }

        // ۲) مهاجرت‌های افزایشیِ ≥11 (idempotent).
        foreach (var res in OrderedScripts(asm, IsMigrationResource))
        {
            var name = ShortName(res);
            if (applied.Contains(name)) continue;
            await ApplyScriptAsync(conn, asm, res, applied, log, ct);
        }
    }

    /// <summary>ورودِ داده‌های دمو (به‌خواستِ کاربر در ویزارد): اسکریپت‌های `*DemoData*` را یک‌بار اجرا می‌کند
    /// (idempotent از طریقِ __AppliedScripts). برای نمایش/آموزش؛ روی DBِ خالیِ تازه‌راه‌اندازی‌شده.</summary>
    public static async Task RunDemoDataAsync(string connectionString, Action<string>? log = null, CancellationToken ct = default)
    {
        var asm = typeof(DatabaseMigrator).Assembly;
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await EnsureTrackingTableAsync(conn, ct);
        var applied = await GetAppliedAsync(conn, ct);
        var demo = asm.GetManifestResourceNames()
            .Where(r => r.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
                     && ShortName(r).Contains("DemoData", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
        foreach (var res in demo)
            await ApplyScriptAsync(conn, asm, res, applied, log, ct);
    }

    /// <summary>
    /// U-MULTI-COMPANY-1 — اسکریپت‌هایِ per-company-safe (CROSS JOIN Cfg.Companies یا
    /// self-referencing از رویِ حساب‌هایِ والدِ موجود) که بلافاصله بعدِ ساختِ یک شرکتِ نو در
    /// runtime دوباره اجرا می‌شوند تا آن شرکت نمودارِ حساب/شعبه/انبار را بگیرد — بدونِ نیازِ
    /// ری‌استارت و **بدونِ توجه به `__AppliedScripts`** (این‌ها باید هر بار که شرکتِ نو ساخته
    /// شد دوباره اجرا شوند، نه فقط یک‌بار در کلِ عمرِ DB مثلِ بقیهٔ مهاجرت‌ها).
    /// </summary>
    private static readonly string[] CompanyProvisioningScripts =
    {
        "68_CompanyBaseChartTemplate.sql",
        "25_InterBranchAccount.sql",
        "26_CashVarianceAccounts.sql",
        "29_PayrollAccounts.sql",
        "61_AccountingCompletionAccounts.sql",
        "62_AdvanceReceiptAccount.sql",
        "64_ConsignmentAccount.sql",
    };

    /// <summary>برایِ شرکتی که تازه ساخته شده: نمودارِ حساب/شعبه/انبارِ پیش‌فرض را seed می‌کند
    /// (فقط شرکت‌هایِ بدونِ این داده تحتِ‌تأثیر قرار می‌گیرند — idempotent per-company).</summary>
    public static async Task RunCompanyProvisioningScriptsAsync(string connectionString, Action<string>? log = null, CancellationToken ct = default)
    {
        var asm = typeof(DatabaseMigrator).Assembly;
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var all = asm.GetManifestResourceNames().Where(r => r.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var scriptName in CompanyProvisioningScripts)
        {
            var res = all.FirstOrDefault(r => ShortName(r).Equals(scriptName, StringComparison.OrdinalIgnoreCase));
            if (res is null) { log?.Invoke($"اسکریپتِ provisioning یافت نشد: {scriptName}"); continue; }
            try
            {
                foreach (var batch in SplitBatches(ReadResource(asm, res)))
                {
                    await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 180 };
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                log?.Invoke($"provisioning اعمال شد: {scriptName}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"provisioning ناموفق (ادامه): {scriptName} — {ex.GetBaseException().Message}");
            }
        }
    }

    /// <summary>یک اسکریپت را batch-به-batch اجرا و در صورتِ موفقیت ثبت می‌کند (شکست → فقط لاگ، ادامه).</summary>
    private static async Task ApplyScriptAsync(SqlConnection conn, Assembly asm, string res,
        HashSet<string> applied, Action<string>? log, CancellationToken ct)
    {
        var name = ShortName(res);
        if (applied.Contains(name)) return;
        try
        {
            foreach (var batch in SplitBatches(ReadResource(asm, res)))
            {
                await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 180 };
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await RecordAppliedAsync(conn, name, ct);
            applied.Add(name);
            log?.Invoke($"اسکریپتِ DB اعمال شد: {name}");
        }
        catch (Exception ex)
        {
            log?.Invoke($"اسکریپتِ DB ناموفق (ادامه): {name} — {ex.GetBaseException().Message}");
        }
    }

    /// <summary>اگر پایگاه‌داده وجود نداشته باشد، با اتصال به master و نامِ پیکربندی‌شده آن را می‌سازد.</summary>
    private static async Task EnsureDatabaseExistsAsync(string connectionString, Action<string>? log, CancellationToken ct)
    {
        string dbName;
        SqlConnectionStringBuilder master;
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            dbName = b.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName)) return;   // نامِ DB مشخص نیست — کاری نمی‌کنیم
            master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        }
        catch { return; }

        try
        {
            await using var mc = new SqlConnection(master.ConnectionString);
            await mc.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "DECLARE @s nvarchar(300) = N'CREATE DATABASE ' + QUOTENAME(@db); " +
                "IF DB_ID(@db) IS NULL EXEC sp_executesql @s;", mc) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("@db", dbName);
            await cmd.ExecuteNonQueryAsync(ct);
            log?.Invoke($"پایگاه‌داده آماده است: {dbName}");
        }
        catch (Exception ex)
        {
            log?.Invoke($"ساختِ پایگاه‌داده ناموفق بود: {ex.GetBaseException().Message}");
        }
    }

    /// <summary>schemaهای پایه را در صورتِ نبود می‌سازد (پیش‌نیازِ اسکریپت‌های پایه که `Acc.*`/`Sec.*`… می‌سازند).</summary>
    private static async Task EnsureSchemasAsync(SqlConnection conn, Action<string>? log, CancellationToken ct)
    {
        foreach (var s in BaseSchemas)
        {
            try
            {
                await using var cmd = new SqlCommand(
                    "DECLARE @sql nvarchar(200) = N'CREATE SCHEMA ' + QUOTENAME(@s); " +
                    "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @s) EXEC sp_executesql @sql;", conn);
                cmd.Parameters.AddWithValue("@s", s);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex) { log?.Invoke($"ساختِ schema {s} ناموفق: {ex.GetBaseException().Message}"); }
        }
    }

    private static async Task<bool> ObjectMissingAsync(SqlConnection conn, string objectName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@o,'U') IS NULL THEN 1 ELSE 0 END", conn);
        cmd.Parameters.AddWithValue("@o", objectName);
        var r = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(r) == 1;
    }

    /// <summary>true اگر جدول وجود نداشته باشد یا خالی باشد (برای تشخیصِ نبودِ داده‌های پایه). نام‌ها ثابت‌اند.</summary>
    private static async Task<bool> TableEmptyAsync(SqlConnection conn, string table, CancellationToken ct)
    {
        if (await ObjectMissingAsync(conn, table, ct)) return true;
        try
        {
            await using var cmd = new SqlCommand($"SELECT CASE WHEN EXISTS(SELECT 1 FROM {table}) THEN 0 ELSE 1 END", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }
        catch { return true; }
    }

    // ── انتخاب/مرتب‌سازیِ منابع ──
    private static List<string> OrderedScripts(Assembly asm, Func<string, bool> filter) =>
        asm.GetManifestResourceNames()
            .Where(filter)
            .OrderBy(MigrationNumber)
            .ThenBy(r => r, StringComparer.Ordinal)
            .ToList();

    /// <summary>اسکریپت‌های پایه: پیشوندِ عددیِ 2..10 (نه ۰۱ که DB-drop/مسیرِ لینوکسی دارد)، بدونِ RunAll/DemoData.</summary>
    private static bool IsBaseResource(string res)
    {
        if (!res.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) return false;
        var name = ShortName(res);
        if (name.Contains("RunAll", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DemoData", StringComparison.OrdinalIgnoreCase)) return false;
        var n = MigrationNumber(res);
        return n >= 2 && n < MinMigrationNumber;
    }

    // ── انتخاب/مرتب‌سازیِ منابع ──
    private static bool IsMigrationResource(string res)
    {
        if (!res.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) return false;
        var name = ShortName(res);
        if (name.Contains("RunAll", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DemoData", StringComparison.OrdinalIgnoreCase)) return false;
        return MigrationNumber(res) >= MinMigrationNumber;
    }

    private static int MigrationNumber(string res)
    {
        var name = ShortName(res);
        return name.Length >= 2 && int.TryParse(name.AsSpan(0, 2), out var n) ? n : -1;
    }

    private static string ShortName(string res)
    {
        var idx = res.IndexOf(ResourceMarker, StringComparison.Ordinal);
        return idx >= 0 ? res[(idx + ResourceMarker.Length)..] : res;
    }

    private static string ReadResource(Assembly asm, string res)
    {
        using var stream = asm.GetManifestResourceStream(res)!;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>تقسیم اسکریپت به batchها روی جداکنندهٔ `GO` و حذفِ `USE …;` (اتصال خودش DBِ درست را هدف گرفته).</summary>
    private static IEnumerable<string> SplitBatches(string sql)
    {
        sql = Regex.Replace(sql, @"^\s*USE\s+\[?\w+\]?\s*;?\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        foreach (var b in batches)
            if (!string.IsNullOrWhiteSpace(b))
                yield return b;
    }

    // ── جدولِ ردیابی ──
    private static async Task EnsureTrackingTableAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = @"
IF OBJECT_ID('dbo.__AppliedScripts','U') IS NULL
CREATE TABLE dbo.__AppliedScripts (
    ScriptName NVARCHAR(255) NOT NULL PRIMARY KEY,
    AppliedAt  DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);";
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<HashSet<string>> GetAppliedAsync(SqlConnection conn, CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand("SELECT ScriptName FROM dbo.__AppliedScripts", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) set.Add(r.GetString(0));
        return set;
    }

    private static async Task RecordAppliedAsync(SqlConnection conn, string name, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "IF NOT EXISTS (SELECT 1 FROM dbo.__AppliedScripts WHERE ScriptName=@n) " +
            "INSERT INTO dbo.__AppliedScripts(ScriptName) VALUES(@n)", conn);
        cmd.Parameters.AddWithValue("@n", name);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
