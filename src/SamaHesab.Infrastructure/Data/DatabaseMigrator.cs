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

    public static async Task RunAsync(string connectionString, Action<string>? log = null, CancellationToken ct = default)
    {
        var asm = typeof(DatabaseMigrator).Assembly;
        var scripts = asm.GetManifestResourceNames()
            .Where(IsMigrationResource)
            .OrderBy(MigrationNumber)
            .ThenBy(r => r, StringComparer.Ordinal)
            .ToList();
        if (scripts.Count == 0) return;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await EnsureTrackingTableAsync(conn, ct);
        var applied = await GetAppliedAsync(conn, ct);

        foreach (var res in scripts)
        {
            var name = ShortName(res);
            if (applied.Contains(name)) continue;

            var sql = ReadResource(asm, res);
            try
            {
                foreach (var batch in SplitBatches(sql))
                {
                    await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                await RecordAppliedAsync(conn, name, ct);
                log?.Invoke($"مهاجرتِ DB اعمال شد: {name}");
            }
            catch (Exception ex)
            {
                // idempotent‌اند؛ شکستِ یکی نباید بقیه/استارتِ اپ را بلاک کند — فقط لاگ می‌شود.
                log?.Invoke($"مهاجرتِ DB ناموفق (ادامه): {name} — {ex.GetBaseException().Message}");
            }
        }
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
