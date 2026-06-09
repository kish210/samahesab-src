namespace SamaHesab.Domain.Enums;

/// <summary>وضعیت میز در سالن</summary>
public enum TableStatus
{
    Free,       // آزاد
    Occupied,   // مشغول (سفارش باز)
    Reserved,   // رزرو شده
    Billing     // در حال تسویه
}

/// <summary>نوع سفارش رستوران</summary>
public enum RestaurantOrderType
{
    DineIn,     // سِرو در سالن (روی میز)
    Takeaway,   // بیرون‌بر
    Delivery    // ارسال با پیک
}

/// <summary>وضعیت کلی سفارش</summary>
public enum RestaurantOrderStatus
{
    Open,       // باز (در حال ثبت)
    Sent,       // ارسال‌شده به آشپزخانه
    Served,     // سرو شده
    Settled,    // تسویه‌شده
    Cancelled   // لغو شده
}

/// <summary>وضعیت یک ردیف سفارش (آیتم غذا)</summary>
public enum OrderItemStatus
{
    Pending,    // در انتظار ارسال به آشپزخانه
    InKitchen,  // ارسال‌شده به آشپزخانه
    Preparing,  // در حال آماده‌سازی
    Ready,      // آماده
    Served,     // سرو شد
    Cancelled   // لغو شد
}

/// <summary>وضعیت رسید آشپزخانه (Kitchen Ticket)</summary>
public enum KitchenTicketStatus
{
    New,        // جدید
    Preparing,  // در حال آماده‌سازی
    Ready,      // آماده تحویل
    Completed   // تکمیل‌شده
}
