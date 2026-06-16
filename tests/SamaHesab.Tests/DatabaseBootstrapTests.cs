using Microsoft.Data.SqlClient;
using SamaHesab.Infrastructure.Data;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// تستِ یکپارچگیِ بوت‌استرپِ پایگاه‌داده (نصبِ تازه): <see cref="DatabaseMigrator.RunAsync"/> باید روی یک
/// DBِ ناموجود، خودِ پایگاه‌داده + schemaها + جدول‌های پایه + seed را بسازد. اگر SQL Serverِ محلی نباشد
/// (مثلاً CI)، تست بی‌صدا رد می‌شود تا شکست ندهد.
/// </summary>
public class DatabaseBootstrapTests
{
    private const string Db = "SamaHesab_BootTest";
    private const string Master = "Server=.\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5;";
    private static string Target => $"Server=.\\SQLEXPRESS;Database={Db};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;Connect Timeout=5;";

    [Fact]
    public async Task Fresh_database_is_created_and_seeded_from_scratch()
    {
        // پیش‌نیاز: SQL Serverِ محلی. در نبودِ آن، رد (بدونِ شکست).
        try { await Exec(Master, "SELECT 1"); }
        catch { return; }

        await DropDb();
        try
        {
            // DB هنوز وجود ندارد — باید توسطِ بوت‌استرپ ساخته شود.
            await DatabaseMigrator.RunAsync(Target, System.Console.WriteLine);

            Assert.True(await Flag(Target, "SELECT CASE WHEN OBJECT_ID('Sec.Users','U')   IS NOT NULL THEN 1 ELSE 0 END"), "Sec.Users باید ساخته شده باشد");
            Assert.True(await Flag(Target, "SELECT CASE WHEN OBJECT_ID('Acc.Accounts','U') IS NOT NULL THEN 1 ELSE 0 END"), "Acc.Accounts باید ساخته شده باشد");
            Assert.True(await Count(Target, "SELECT COUNT(*) FROM Acc.Accounts")  > 0, "نمودار حساب‌ها باید seed شده باشد");
            Assert.True(await Count(Target, "SELECT COUNT(*) FROM Cfg.Companies") > 0, "شرکتِ پیش‌فرض باید seed شده باشد");

            // اجرای دوباره باید idempotent باشد (نباید استثنا/خطا بدهد).
            await DatabaseMigrator.RunAsync(Target, System.Console.WriteLine);
            Assert.True(await Count(Target, "SELECT COUNT(*) FROM Cfg.Companies") > 0);
        }
        finally { await DropDb(); }
    }

    private static async Task DropDb() => await Exec(Master,
        $"IF DB_ID('{Db}') IS NOT NULL BEGIN ALTER DATABASE [{Db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Db}]; END");

    private static async Task Exec(string cs, string sql)
    { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c); await cmd.ExecuteNonQueryAsync(); }

    private static async Task<bool> Flag(string cs, string sql)
    { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c); return System.Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1; }

    private static async Task<int> Count(string cs, string sql)
    { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand(sql, c); return System.Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
}
