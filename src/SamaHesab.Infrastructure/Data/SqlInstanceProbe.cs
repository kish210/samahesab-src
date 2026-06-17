using Microsoft.Data.SqlClient;

namespace SamaHesab.Infrastructure.Data;

/// <summary>
/// یابندهٔ خودکارِ نمونهٔ SQL Server (نصبِ تازه روی سیستمِ ناشناخته).
/// مشکلِ ریشه‌ای: نامِ نمونهٔ SQL روی هر سیستم فرق دارد (`.\SQLEXPRESS`، `.`، LocalDB، …)؛
/// اگر رشتهٔ اتصالِ پیکربندی‌شده وصل نشود، نمونه‌های رایج را امتحان می‌کند و اولین نمونهٔ
/// کارا را برمی‌گرداند — تا نصبِ تازه «خودکار» کار کند بدونِ این‌که کاربر نامِ نمونه را بداند.
/// </summary>
public static class SqlInstanceProbe
{
    // پرتکرارترین نمونه‌های محلیِ SQL روی ویندوز (به‌ترتیبِ احتمال).
    private static readonly string[] Candidates =
    {
        @".\SQLEXPRESS", @"localhost\SQLEXPRESS", ".", "localhost",
        @"(localdb)\MSSQLLocalDB", @".\MSSQLSERVER", @".\SQL2019", @".\SQL2022"
    };

    /// <summary>
    /// اگر سرورِ رشتهٔ اتصال وصل شود، همان را برمی‌گرداند؛ وگرنه نمونه‌های رایج را می‌آزماید و
    /// رشتهٔ اتصالِ نمونهٔ کارا را برمی‌گرداند. اگر هیچ‌کدام وصل نشدند، همان ورودی را برمی‌گرداند.
    /// </summary>
    public static async Task<string> ResolveAsync(string connectionString, Action<string>? log = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        SqlConnectionStringBuilder b;
        try { b = new SqlConnectionStringBuilder(connectionString); } catch { return connectionString; }

        if (await CanConnectAsync(b, ct)) return connectionString;   // سرورِ پیکربندی‌شده کار می‌کند

        var current = b.DataSource;
        foreach (var srv in Candidates)
        {
            if (string.Equals(srv, current, StringComparison.OrdinalIgnoreCase)) continue;
            SqlConnectionStringBuilder t;
            try { t = new SqlConnectionStringBuilder(connectionString) { DataSource = srv }; } catch { continue; }
            if (await CanConnectAsync(t, ct))
            {
                log?.Invoke($"نمونهٔ SQL یافت شد: {srv} (به‌جای «{current}»)");
                return t.ConnectionString;
            }
        }
        log?.Invoke($"هیچ نمونهٔ SQLِ کارا یافت نشد (پیکربندی‌شده: «{current}»).");
        return connectionString;
    }

    /// <summary>تلاش برای اتصال به master روی همان سرور با تایم‌اوتِ کوتاه (بدونِ نیاز به وجودِ DBِ هدف).</summary>
    private static async Task<bool> CanConnectAsync(SqlConnectionStringBuilder src, CancellationToken ct)
    {
        try
        {
            var m = new SqlConnectionStringBuilder(src.ConnectionString)
            { InitialCatalog = "master", ConnectTimeout = 3 };
            await using var c = new SqlConnection(m.ConnectionString);
            await c.OpenAsync(ct);
            return true;
        }
        catch { return false; }
    }
}
