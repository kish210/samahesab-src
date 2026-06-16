-- =============================================================================
-- فاز ۱۲ P-G9 — ایندکس‌های کارایی برای حجمِ واقعی (هزاران کالا/فاکتور/سند).
-- روی ستون‌های پرترافیکِ کوئری‌های FIFO/کاردکس · تراز/دفتر · فهرستِ فاکتور/مشتری.
-- idempotent: هر ایندکس فقط اگر نبود ساخته می‌شود (امن برای migration-runner).
-- =============================================================================
USE SamaHesab;
GO

-- کاردکس/FIFO: فیلتر بر شرکت+کالا+انبار، ترتیب بر CreatedAt (داغ‌ترین مسیرِ موجودی)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_StockTransactions_Prod_Wh')
    CREATE INDEX IX_StockTransactions_Prod_Wh
        ON Inv.StockTransactions (CompanyId, ProductId, WarehouseId, CreatedAt);
GO

-- تراز/دفتر/فهرستِ اسناد بر بازهٔ تاریخ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Vouchers_Company_Date')
    CREATE INDEX IX_Vouchers_Company_Date
        ON Acc.Vouchers (CompanyId, VoucherDate);
GO

-- جمع‌بندیِ تراز/دفترِ کل بر حساب (VoucherItems → Account)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_VoucherItems_Account')
    CREATE INDEX IX_VoucherItems_Account
        ON Acc.VoucherItems (AccountId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_VoucherItems_Voucher')
    CREATE INDEX IX_VoucherItems_Voucher
        ON Acc.VoucherItems (VoucherId);
GO

-- فهرست/صورت‌حسابِ فروش بر مشتری و تاریخ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SalesInvoices_Customer')
    CREATE INDEX IX_SalesInvoices_Customer
        ON Sal.SalesInvoices (CompanyId, CustomerId, InvoiceDate);
GO

-- فهرست/صورت‌حسابِ خرید بر تأمین‌کننده و تاریخ
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PurchaseInvoices_Supplier')
    CREATE INDEX IX_PurchaseInvoices_Supplier
        ON Pur.PurchaseInvoices (CompanyId, SupplierId, InvoiceDate);
GO

-- جست‌وجوی کد در فهرست‌ها (مشتری/تأمین‌کننده/کالا)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Customers_Company_Code')
    CREATE INDEX IX_Customers_Company_Code ON Crm.Customers (CompanyId, Code);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Suppliers_Company_Code')
    CREATE INDEX IX_Suppliers_Company_Code ON Crm.Suppliers (CompanyId, Code);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Products_Company_Code')
    CREATE INDEX IX_Products_Company_Code ON Inv.Products (CompanyId, Code);
GO
