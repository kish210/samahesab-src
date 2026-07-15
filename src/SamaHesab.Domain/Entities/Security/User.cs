using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Security;

/// <summary>Application user (maps to Sec.Users).</summary>
public class User : BaseEntity
{
    public int CompanyId { get; private set; }
    public int? BranchId { get; private set; }
    public string Username { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string PasswordSalt { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Mobile { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsLocked { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool MustChangePass { get; private set; }
    /// <summary>U-SEC-RECOVERY — هشِ کدِ بازیابیِ رمز (همان الگویِ PasswordHash/Salt، هرگز خام).
    /// یک‌بار در ویزاردِ راه‌اندازیِ اولیه ساخته و به کاربر نشان داده می‌شود؛ اگر رمز فراموش شود،
    /// از صفحهٔ ورود با همین کد می‌شود رمزِ جدید تعیین کرد (بدونِ نیازِ ایمیل/پیامک).</summary>
    public string? RecoveryCodeHash { get; private set; }
    public string? RecoveryCodeSalt { get; private set; }
    public bool HasRecoveryCode => !string.IsNullOrEmpty(RecoveryCodeHash);
    /// <summary>SP-1 — نگاشتِ کاربر به «فروشنده» (Crm.Parties). اگر مقدار داشته باشد، فروشِ گردشگری
    /// فروشنده را خودکار از همین کاربر تشخیص می‌دهد (پنلِ فروشنده‌محور).</summary>
    public int? SalespersonPartyId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; private set; }

    private User() { }

    public static User Create(int companyId, int? branchId, string username,
        string passwordHash, string passwordSalt, string fullName, string? email = null, string? mobile = null)
        => new()
        {
            CompanyId = companyId,
            BranchId = branchId,
            Username = username,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            FullName = fullName,
            Email = email,
            Mobile = mobile
        };

    public void RecordSuccessfulLogin()
    {
        FailedAttempts = 0;
        LastLoginAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    public void RecordFailedAttempt()
    {
        FailedAttempts++;
        if (FailedAttempts >= 5) { IsLocked = true; LockoutEnd = DateTime.Now.AddMinutes(15); }
        UpdatedAt = DateTime.Now;
    }

    public void SetPassword(string hash, string salt)
    {
        PasswordHash = hash; PasswordSalt = salt; MustChangePass = false; UpdatedAt = DateTime.Now;
    }

    public void Unlock()
    {
        IsLocked = false; LockoutEnd = null; FailedAttempts = 0; UpdatedAt = DateTime.Now;
    }

    /// <summary>SR-6 — نشان‌گذاریِ کاربر برای اجبارِ تغییرِ رمز در اولینِ ورودِ بعدی
    /// (مثلاً ادمینِ تازه‌ساخته‌شده با رمزِ پیش‌فرض).</summary>
    public void RequirePasswordChangeOnNextLogin()
    {
        MustChangePass = true; UpdatedAt = DateTime.Now;
    }

    /// <summary>SP-1 — تنظیمِ نگاشتِ فروشنده (یا حذفِ آن با null).</summary>
    public void SetSalesperson(int? partyId)
    {
        SalespersonPartyId = partyId is > 0 ? partyId : null;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>U-SEC-RECOVERY — ذخیرهٔ هشِ کدِ بازیابیِ نو (خودِ کد هرگز اینجا نیست).</summary>
    public void SetRecoveryCode(string hash, string salt)
    {
        RecoveryCodeHash = hash; RecoveryCodeSalt = salt; UpdatedAt = DateTime.Now;
    }
}
