namespace SamaHesab.Domain.Enums;

/// <summary>🆘 HC-2 — شدتِ باگ.</summary>
public enum BugSeverity { Low = 0, Medium = 1, High = 2, Critical = 3 }

/// <summary>🆘 HC-2 — دستهٔ گزارش/تیکت (ماژولِ مرتبط).</summary>
public enum SupportCategory
{
    Accounting = 0, Treasury = 1, Sales = 2, Purchase = 3, Inventory = 4,
    Reports = 5, Security = 6, Pos = 7, Restaurant = 8, Tourism = 9,
    System = 10, Other = 11
}

/// <summary>🆘 HC-2 — وضعیتِ تیکت/گزارش (چرخهٔ کاملِ پشتیبانی).</summary>
public enum SupportStatus
{
    New = 0, Open = 1, InProgress = 2, WaitingCustomer = 3,
    Testing = 4, Resolved = 5, Closed = 6
}

/// <summary>🆘 HC-2 — اولویتِ درخواستِ قابلیت.</summary>
public enum FeaturePriority { Low = 0, Medium = 1, High = 2 }

/// <summary>🆘 HC-2 — وضعیتِ نشستِ پشتیبانیِ ریموت.</summary>
public enum RemoteSessionStatus { Pending = 0, Active = 1, Ended = 2, Expired = 3 }

/// <summary>🆘 HC-2 — وضعیتِ همگام‌سازی با سرورِ پشتیبانی (وردپرس).</summary>
public enum SyncState { Local = 0, Queued = 1, Synced = 2, Failed = 3 }
