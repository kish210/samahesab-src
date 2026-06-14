using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.CRM;

/// <summary>کارِ ۹ — سندِ ضمیمهٔ مشتری. فایل در سیستم‌فایلِ محلی ذخیره می‌شود؛ این موجودیت فقط متادیتا را نگه می‌دارد.</summary>
public class CustomerAttachment : BaseEntity
{
    public int CompanyId { get; private set; }
    public int CustomerId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string StoredPath { get; private set; } = default!;   // مسیرِ کاملِ فایلِ کپی‌شده
    public string? ContentType { get; private set; }
    public long FileSize { get; private set; }
    public string UploadedAt { get; private set; } = default!;    // تاریخ شمسی yyyy/MM/dd
    public string? Description { get; private set; }

    private CustomerAttachment() { }

    public static CustomerAttachment Create(int companyId, int customerId, string fileName,
        string storedPath, string? contentType, long fileSize, string uploadedAt, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("نام فایل الزامی است.");
        if (string.IsNullOrWhiteSpace(storedPath)) throw new ArgumentException("مسیر فایل الزامی است.");
        return new CustomerAttachment
        {
            CompanyId = companyId,
            CustomerId = customerId,
            FileName = fileName,
            StoredPath = storedPath,
            ContentType = contentType,
            FileSize = fileSize,
            UploadedAt = uploadedAt,
            Description = description
        };
    }
}
