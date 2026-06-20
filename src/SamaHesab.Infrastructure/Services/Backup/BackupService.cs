using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.Infrastructure.Services.Backup;

public class BackupService : IBackupService
{
    private readonly string _connectionString;
    private readonly ILogger<BackupService> _logger;
    private readonly string _defaultBackupPath;

    public BackupService(IConfiguration config, ILogger<BackupService> logger)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("رشته اتصال پایگاه داده یافت نشد.");
        _logger = logger;
        _defaultBackupPath = config["Backup:Path"] ?? @"C:\SamaHesabBackup";
    }

    public async Task<string> BackupAsync(string? backupPath = null, CancellationToken ct = default)
    {
        var path = backupPath ?? _defaultBackupPath;
        Directory.CreateDirectory(path);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = Path.Combine(path, $"SamaHesab_{timestamp}.bak");

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            // فشرده‌سازی روی نسخهٔ Express پشتیبانی نمی‌شود (EngineEdition=4) → فقط در نسخه‌های دیگر.
            var edition = Convert.ToInt32(await new SqlCommand(
                "SELECT SERVERPROPERTY('EngineEdition')", conn).ExecuteScalarAsync(ct));
            var withClause = edition == 4 ? "WITH FORMAT, STATS = 10" : "WITH COMPRESSION, FORMAT, STATS = 10";
            var sql = $"BACKUP DATABASE [SamaHesab] TO DISK = N'{fileName.Replace("'", "''")}' {withClause}";

            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 3600 };
            await cmd.ExecuteNonQueryAsync(ct);

            // ثبت در تاریخچهٔ پشتیبان (best-effort تا فهرست به‌روز بماند).
            try
            {
                long size = File.Exists(fileName) ? new FileInfo(fileName).Length : 0;
                await using var h = new SqlCommand(
                    "INSERT INTO Cfg.BackupHistory(BackupType, FilePath, FileSize, Status, CreatedAt) " +
                    "VALUES (N'دستی', @p, @s, N'موفق', SYSDATETIME())", conn);
                h.Parameters.AddWithValue("@p", fileName);
                h.Parameters.AddWithValue("@s", size);
                await h.ExecuteNonQueryAsync(ct);
            }
            catch (Exception hx) { _logger.LogWarning(hx, "ثبتِ تاریخچهٔ پشتیبان ناموفق بود"); }

            _logger.LogInformation("پشتیبان‌گیری با موفقیت انجام شد: {FileName}", fileName);
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در پشتیبان‌گیری از پایگاه داده");
            throw new InvalidOperationException($"پشتیبان‌گیری ناموفق بود: {ex.Message}", ex);
        }
    }

    public async Task RestoreAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
            throw new FileNotFoundException("فایل پشتیبان یافت نشد.", backupFilePath);

        var sql = $@"
            USE master;
            ALTER DATABASE [SamaHesab] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            RESTORE DATABASE [SamaHesab] FROM DISK = N'{backupFilePath}' WITH REPLACE, STATS = 10;
            ALTER DATABASE [SamaHesab] SET MULTI_USER;";

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 7200 };
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("بازیابی پایگاه داده با موفقیت انجام شد از: {FilePath}", backupFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در بازیابی پایگاه داده");
            throw new InvalidOperationException($"بازیابی ناموفق بود: {ex.Message}", ex);
        }
    }

    public async Task<IReadOnlyList<BackupInfo>> GetBackupHistoryAsync(CancellationToken ct = default)
    {
        var sql = @"SELECT TOP 50 Id, BackupType, FilePath, FileSize, Status, CreatedAt
                    FROM Cfg.BackupHistory ORDER BY CreatedAt DESC";
        var results = new List<BackupInfo>();
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                results.Add(new BackupInfo(
                    reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    reader.GetString(4), reader.GetDateTime(5)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در دریافت تاریخچه پشتیبان‌گیری");
        }
        return results;
    }

    public async Task<string?> AutoBackupAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("شروع پشتیبان‌گیری خودکار...");
            var file = await BackupAsync(ct: ct);

            // Cleanup old backups (keep last 30)
            var files = Directory.GetFiles(_defaultBackupPath, "SamaHesab_*.bak")
                                  .OrderByDescending(f => f).Skip(30).ToList();
            foreach (var f in files) { File.Delete(f); _logger.LogInformation("حذف پشتیبان قدیمی: {File}", f); }
            return file;
        }
        catch (Exception ex) { _logger.LogError(ex, "خطا در پشتیبان‌گیری خودکار"); return null; }
    }
}
