namespace SamaHesab.Domain.Enums;

public enum VoucherStatus
{
    Draft = 1,       // پیش‌نویس
    Posted = 2,      // قطعی
    Permanent = 3    // دائمی
}

/// <summary>وضعیتِ گردش‌کارِ تأییدِ سند (T22). null روی سند = خارج از گردش‌کار (قابلِ قطعیِ مستقیم).</summary>
public enum VoucherApprovalStatus
{
    PendingApproval = 1,   // در انتظار تأیید
    Approved = 2,          // تأییدشده
    Rejected = 3           // ردشده
}

public enum AccountLevel
{
    Group = 1,       // گروه
    General = 2,     // کل
    Subsidiary = 3,  // معین
    Detail = 4       // تفصیلی
}

public enum AccountNature
{
    Debit,   // بدهکار
    Credit   // بستانکار
}

public enum ChequeType
{
    Received,  // دریافتی
    Paid       // پرداختی
}

public enum ChequeStatus
{
    InProcess,    // در جریان
    Cleared,      // وصول شده
    Returned,     // برگشت خورده
    Transferred,  // واگذار شده
    Cancelled     // ابطال شده
}

public enum PaymentType
{
    Cash,         // نقدی
    Card,         // کارتخوان
    Cheque,       // چک
    Transfer,     // حواله
    Installment   // اقساط
}

/// <summary>روشِ استهلاکِ داراییِ ثابت (راهکاران: استهلاک هم‌زمان با چند روش/استاندارد).</summary>
public enum DepreciationMethod
{
    StraightLine,      // خط مستقیم
    DecliningBalance   // نزولی (ماندهٔ کاهنده)
}
