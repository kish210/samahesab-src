-- =============================================================================
-- U-PARTY-BAL — بازمحاسبهٔ Party.Balance از رویِ فاکتورهایِ واقعیِ ثبت‌شده
-- =============================================================================
-- ⚠️ این فایل عمداً بیرونِ پوشهٔ database/ است — به‌صورتِ خودکار توسطِ برنامه
--    اجرا نمی‌شود (برخلافِ database/*.sql که در استارتاپ اجرا می‌شوند).
--    این اسکریپت باید دستی، فقط با تصمیمِ آگاهانهٔ کاربر، اجرا شود.
--
-- زمینه: تا نسخهٔ ۲.۸.۱۶، فقط CreateReceiptCommand/CreatePaymentCommand مقدارِ
-- Party.Balance را (با دریافت/پرداختِ خزانه) کم می‌کردند؛ هیچ‌کدام از فرمان‌هایِ
-- صدورِ فاکتور (فروش/برگشتِ فروش/خرید/برگشتِ خرید) بدهیِ نسیهٔ جدید را به آن
-- اضافه نمی‌کردند. یعنی Balanceِ ذخیره‌شده برایِ هر مشتری/تأمین‌کننده‌ای که پیش
-- از ۲.۸.۱۶ فعالیتِ نسیه داشته، از بدهیِ واقعی عقب مانده است (فقط کم شده،
-- هرگز اضافه نشده). نسخهٔ ۲.۸.۱۶ این را برایِ فاکتورهایِ *جدید* رفع کرد، ولی
-- دادهٔ تاریخیِ موجود را خودکار اصلاح نکرد.
--
-- این اسکریپت مانده را از رویِ مجموعِ RemainAmount فاکتورهایِ ثبت‌شده بازمحاسبه
-- می‌کند:
--     NewBalance = Σ(فروش.RemainAmount) − Σ(برگشت‌ازفروش.RemainAmount)
--                + Σ(خرید.RemainAmount)  − Σ(برگشتِ‌خرید.RemainAmount)
--
-- ⚠️ محدودیتِ صادقانه: برایِ برگشت‌هایِ تاریخی که از مسیرِ مستقلِ قدیمِ
-- CreateSalesReturnCommand/CreatePurchaseReturnCommand آمده‌اند (پیش از رفعِ
-- امروز)، PaidAmount هرگز ست نمی‌شد؛ یعنی RemainAmountِ آن‌ها همیشه برابرِ
-- GrandTotال است — چه بازپرداختِ نقدی بوده باشد چه نسیه/اعتباری. اگر برگشتی
-- واقعاً نقدی بازپرداخت شده بود، این بازمحاسبه آن را به‌اشتباه به‌عنوانِ
-- «کاهشِ بدهیِ اعتباری» حساب می‌کند (تقریب، نه دقتِ صددرصد). حجمِ این خطا را
-- می‌توان با شمارشِ ردیف‌هایِ زیر تخمین زد:
--   SELECT COUNT(*) FROM Sal.SalesInvoices WHERE InvoiceType=N'برگشت از فروش' AND PaidAmount=0;
--   SELECT COUNT(*) FROM Pur.PurchaseInvoices WHERE InvoiceType=N'برگشت خرید' AND PaidAmount=0;
--
-- ── مرحلهٔ ۱ (فقط گزارش — امن، هیچ داده‌ای تغییر نمی‌کند) ──────────────────
-- این SELECT را اول اجرا کن و مانده‌هایِ قدیم/جدید/تفاوت را مرور کن.
USE SamaHesab;
GO

WITH SalesNet AS (
    SELECT CustomerId,
           SUM(CASE WHEN InvoiceType = N'فروش' THEN GrandTotal - PaidAmount ELSE 0 END)
         - SUM(CASE WHEN InvoiceType = N'برگشت از فروش' THEN GrandTotal - PaidAmount ELSE 0 END) AS Amt
    FROM Sal.SalesInvoices
    WHERE InvoiceType IN (N'فروش', N'برگشت از فروش')
    GROUP BY CustomerId
),
PurchaseNet AS (
    SELECT SupplierId,
           SUM(CASE WHEN InvoiceType = N'خرید' THEN GrandTotal - PaidAmount ELSE 0 END)
         - SUM(CASE WHEN InvoiceType = N'برگشت خرید' THEN GrandTotal - PaidAmount ELSE 0 END) AS Amt
    FROM Pur.PurchaseInvoices
    WHERE InvoiceType IN (N'خرید', N'برگشت خرید')
    GROUP BY SupplierId
)
SELECT
    p.Id,
    LTRIM(RTRIM(ISNULL(p.FirstName, N'') + N' ' + ISNULL(p.LastName, N''))) AS Name,
    p.IsCustomer, p.IsSupplier,
    p.Balance AS OldBalance,
    ROUND(ISNULL(sn.Amt, 0) + ISNULL(pn.Amt, 0), 2) AS NewComputedBalance,
    ROUND((ISNULL(sn.Amt, 0) + ISNULL(pn.Amt, 0)) - p.Balance, 2) AS Delta
FROM CRM.Parties p
LEFT JOIN SalesNet sn ON sn.CustomerId = p.Id
LEFT JOIN PurchaseNet pn ON pn.SupplierId = p.Id
WHERE (sn.Amt IS NOT NULL OR pn.Amt IS NOT NULL)   -- فقط طرف‌حساب‌هایِ دارایِ فعالیتِ واقعیِ فاکتوری
ORDER BY ABS((ISNULL(sn.Amt, 0) + ISNULL(pn.Amt, 0)) - p.Balance) DESC;
GO

-- ── مرحلهٔ ۲ (اعمالِ واقعی — فقط بعدِ مرورِ کاملِ گزارشِ بالا و تأییدِ صریحِ کاربر) ──
-- عمداً کامنت است. برایِ اجرا: انتخاب و اجرایِ دستیِ بلاکِ زیر (نه کلِ فایل).
/*
;WITH SalesNet AS (
    SELECT CustomerId,
           SUM(CASE WHEN InvoiceType = N'فروش' THEN GrandTotal - PaidAmount ELSE 0 END)
         - SUM(CASE WHEN InvoiceType = N'برگشت از فروش' THEN GrandTotal - PaidAmount ELSE 0 END) AS Amt
    FROM Sal.SalesInvoices
    WHERE InvoiceType IN (N'فروش', N'برگشت از فروش')
    GROUP BY CustomerId
),
PurchaseNet AS (
    SELECT SupplierId,
           SUM(CASE WHEN InvoiceType = N'خرید' THEN GrandTotal - PaidAmount ELSE 0 END)
         - SUM(CASE WHEN InvoiceType = N'برگشت خرید' THEN GrandTotal - PaidAmount ELSE 0 END) AS Amt
    FROM Pur.PurchaseInvoices
    WHERE InvoiceType IN (N'خرید', N'برگشت خرید')
    GROUP BY SupplierId
)
UPDATE p
SET p.Balance = ROUND(ISNULL(sn.Amt, 0) + ISNULL(pn.Amt, 0), 2),
    p.UpdatedAt = GETDATE()
FROM CRM.Parties p
LEFT JOIN SalesNet sn ON sn.CustomerId = p.Id
LEFT JOIN PurchaseNet pn ON pn.SupplierId = p.Id
WHERE (sn.Amt IS NOT NULL OR pn.Amt IS NOT NULL);
*/
