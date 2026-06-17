using Microsoft.Data.SqlClient;
using SamaHesab.Infrastructure.Data;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// تستِ یابندهٔ خودکارِ نمونهٔ SQL (<see cref="SqlInstanceProbe"/>): اگر سرورِ پیکربندی‌شده غلط/ناموجود
/// باشد، باید به یک نمونهٔ کارای رایج (مثلِ .\SQLEXPRESS) fallback کند. در نبودِ SQLِ محلی، بی‌صدا رد می‌شود.
/// </summary>
public class SqlInstanceProbeTests
{
    private const string Master = "Server=.\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5;";

    [Fact]
    public async Task Resolve_falls_back_to_a_working_instance_when_configured_server_is_wrong()
    {
        // پیش‌نیاز: SQL Serverِ محلی (.\SQLEXPRESS). در نبودِ آن (CI) رد می‌شود.
        try { await using var c = new SqlConnection(Master); await c.OpenAsync(); }
        catch { return; }

        var bad = "Server=.\\NONEXISTENT_XYZ_999;Database=SamaHesab;Trusted_Connection=True;" +
                  "TrustServerCertificate=True;Encrypt=False;Connect Timeout=3;";

        var resolved = await SqlInstanceProbe.ResolveAsync(bad);

        Assert.NotEqual(bad, resolved);   // باید سرورِ دیگری پیدا کرده باشد

        // نمونهٔ یافته‌شده باید واقعاً قابلِ اتصال باشد (وگرنه fallback بی‌فایده است).
        var b = new SqlConnectionStringBuilder(resolved) { InitialCatalog = "master", ConnectTimeout = 5 };
        await using var verify = new SqlConnection(b.ConnectionString);
        await verify.OpenAsync();   // نباید استثنا بدهد
    }

    [Fact]
    public async Task Resolve_keeps_a_working_connection_string_unchanged()
    {
        try { await using var c = new SqlConnection(Master); await c.OpenAsync(); }
        catch { return; }

        var good = "Server=.\\SQLEXPRESS;Database=SamaHesab;Trusted_Connection=True;" +
                   "TrustServerCertificate=True;Encrypt=False;Connect Timeout=5;";
        var resolved = await SqlInstanceProbe.ResolveAsync(good);
        Assert.Equal(good, resolved);   // سرورِ کارا نباید تغییر کند
    }
}
