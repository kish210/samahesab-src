namespace SamaHesab.Application.Common.Interfaces;

public interface IBackupService
{
    Task<string> BackupAsync(string? backupPath = null, CancellationToken ct = default);
    Task RestoreAsync(string backupFilePath, CancellationToken ct = default);
    Task<IReadOnlyList<BackupInfo>> GetBackupHistoryAsync(CancellationToken ct = default);
    Task AutoBackupAsync(CancellationToken ct = default);   // RC-3 — پشتیبان + پاک‌سازیِ قدیمی‌ها
}

public record BackupInfo(
    int Id,
    string BackupType,
    string FilePath,
    long? FileSize,
    string Status,
    DateTime CreatedAt
);
