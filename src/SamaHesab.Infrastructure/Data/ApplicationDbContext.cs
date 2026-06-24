using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Application.Common.Events;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.HRM;

namespace SamaHesab.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    // ── Multi-tenancy (Phase 0): scope every AuditableEntity to the current company ──
    // مقدارها در سازنده از کاربر جاری خوانده می‌شوند؛ EF این ارجاع‌ها را per-query پارامتری می‌کند.
    private readonly int _companyId;
    private readonly bool _tenantFilterEnabled;

    // ── Multi-branch (MB-1 گام۲): جداسازیِ secure-by-default دادهٔ شعبه ──
    // کاربرِ دارای مجوز «Security.AllBranches» (یا ADMIN) همهٔ شعب را می‌بیند؛ بقیه فقط شعبهٔ خود را.
    private readonly int _branchId;
    private readonly bool _branchScopeEnabled;

    // ── ماژولارسازی (G4): مدلِ EFِ ماژول‌های نصب‌شده/فعال از DI می‌آید؛ هسته موجودیتِ ماژول را hard-code نمی‌کند. ──
    private readonly IReadOnlyList<IModule> _modules;

    /// <summary>کلیدِ مجموعهٔ ماژول‌های فعال — تا کشِ مدلِ EF با تغییرِ ماژول‌ها بازساخته شود (removability).</summary>
    public string ActiveModuleKeys => string.Join(",", _modules.Select(m => m.Key).OrderBy(k => k, System.StringComparer.Ordinal));

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPublisher publisher,
        ICurrentUserService? currentUser = null, IEnumerable<IModule>? modules = null)
        : base(options)
    {
        _modules = modules?.ToList() ?? new List<IModule>();
        _publisher = publisher;
        _companyId = currentUser?.CompanyId ?? 0;
        // وقتی کاربری احراز نشده (seeding/ورود/سرویس‌های پس‌زمینه) فیلتر غیرفعال است تا چیزی نشکند.
        _tenantFilterEnabled = _companyId > 0;

        _branchId = currentUser?.BranchId ?? 0;
        var seesAllBranches = currentUser?.HasPermission("Security", "AllBranches", "") ?? true;
        // فقط وقتی کاربرِ احرازشده‌ای هست که مجوز همه‌شعبه ندارد، فیلتر شعبه فعال می‌شود.
        _branchScopeEnabled = _tenantFilterEnabled && _branchId > 0 && !seesAllBranches;
    }

    /// <summary>فیلتر ترکیبیِ شرکت + شعبه برای موجودیت‌های شعبه‌ای (مثل سند).</summary>
    private void ApplyTenantAndBranchFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditableEntity, IBranchScoped
        => modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => (!_tenantFilterEnabled || e.CompanyId == _companyId)
                              && (!_branchScopeEnabled || e.BranchId == _branchId));

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : AuditableEntity
        => modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => !_tenantFilterEnabled || e.CompanyId == _companyId);

    // Accounting
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<VoucherItem> VoucherItems { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Cheque> Cheques { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<VoucherTemplate> VoucherTemplates { get; set; }
    public DbSet<VoucherTemplateLine> VoucherTemplateLines { get; set; }
    public DbSet<RecurringVoucher> RecurringVouchers { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<CostCenter> CostCenters { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Security.Role> Roles { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Security.RolePermission> RolePermissions { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Security.UserRole> UserRoles { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Settings.UserItemRef> UserItemRefs { get; set; }
    public DbSet<SamaHesab.Domain.Entities.POS.CashShift> CashShifts { get; set; }
    public DbSet<SamaHesab.Domain.Entities.POS.HeldSale> HeldSales { get; set; }
    public DbSet<StockCountSession> StockCountSessions { get; set; }
    public DbSet<StockCountLine> StockCountLines { get; set; }

    // Inventory
    public DbSet<Product> Products { get; set; }
    public DbSet<StockItem> StockItems { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Inventory.ProductDiscountTier> ProductDiscountTiers { get; set; }

    // CRM
    public DbSet<SamaHesab.Domain.Entities.CRM.CustomerAttachment> CustomerAttachments { get; set; }
    public DbSet<Party> Parties { get; set; }   // طرف‌حساب یکپارچه (Customer+Supplier)

    // Sales
    public DbSet<SalesInvoice> SalesInvoices { get; set; }
    public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
    public DbSet<RecurringInvoice> RecurringInvoices { get; set; }
    public DbSet<RecurringInvoiceLine> RecurringInvoiceLines { get; set; }

    // Purchase
    public DbSet<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice> PurchaseInvoices { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Purchase.PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Purchase.PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Purchase.PurchaseOrderItem> PurchaseOrderItems { get; set; }

    // HRM
    public DbSet<Employee> Employees { get; set; }
    public DbSet<SamaHesab.Domain.Entities.HRM.Department> Departments { get; set; }
    // AttendanceRecord و سایر موجودیت‌های حقوق/حضور به Modules.HR منتقل شدند (context.Set<T> از طریقِ مدلِ ماژول).

    // Restaurant (v2) → استخراج شد به SamaHesab.Modules.Restaurant؛ DbSet/مپ از RestaurantModule.
    public DbSet<SamaHesab.Domain.Entities.Documents.DocumentTemplate> DocumentTemplates { get; set; }   // فاز ۱۰ DT-2

    // ─── 🆘 HC-2 — مرکزِ پشتیبانی (schema Sup) ──────────────────────────────
    public DbSet<SamaHesab.Domain.Entities.Support.BugReport> BugReports { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Support.FeatureRequest> FeatureRequests { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Support.SupportTicket> SupportTickets { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Support.TicketMessage> TicketMessages { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Support.KnowledgeArticle> KnowledgeArticles { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Support.ReleaseNote> ReleaseNotes { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Support.RemoteSupportSession> RemoteSupportSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DomainEvents are an in-memory concern, never persisted. Stop EF from
        // trying to map the BaseEntity.DomainEvents collection as an entity.
        modelBuilder.Ignore<SamaHesab.Domain.Common.DomainEvent>();

        // Apply detailed IEntityTypeConfiguration<T> classes in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Map each entity to its REAL schema-qualified table (the DB was created
        // by the SQL scripts in /database with schemas Acc/Inv/Crm/Sal).
        modelBuilder.Entity<Account>().ToTable("Accounts", "Acc");
        modelBuilder.Entity<Voucher>().ToTable("Vouchers", "Acc");
        modelBuilder.Entity<VoucherItem>().ToTable("VoucherItems", "Acc");
        modelBuilder.Entity<Cheque>().ToTable("Cheques", "Acc");
        modelBuilder.Entity<BankAccount>().ToTable("BankAccounts", "Acc");
        modelBuilder.Entity<Product>().ToTable("Products", "Inv");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Inventory.ProductGroup>(b =>
        {
            b.ToTable("ProductGroups", "Inv");
            b.Ignore(g => g.Parent);
            b.Ignore(g => g.Children);
            b.Ignore(g => g.CreatedAt);   // Inv.ProductGroups has no audit-date columns
            b.Ignore(g => g.UpdatedAt);
        });
        modelBuilder.Entity<Warehouse>().ToTable("Warehouses", "Inv");
        // Inv.Warehouses has no CreatedAt/UpdatedAt columns.
        modelBuilder.Entity<Warehouse>().Ignore(w => w.CreatedAt);
        modelBuilder.Entity<Warehouse>().Ignore(w => w.UpdatedAt);
        modelBuilder.Entity<StockItem>().ToTable("StockItems", "Inv");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Inventory.StockTransaction>(b =>
        {
            b.ToTable("StockTransactions", "Inv");
            b.HasKey(t => t.Id);
        });
        // پلهٔ تخفیفِ مقداریِ کالا (U6)
        modelBuilder.Entity<SamaHesab.Domain.Entities.Inventory.ProductDiscountTier>().ToTable("ProductDiscountTiers", "Inv");

        // بچ و سریال (عمق انبار — INV-1): جداول Inv.Batches/Serials از قبل در SQL هست.
        modelBuilder.Entity<SamaHesab.Domain.Entities.Inventory.Batch>(b =>
        {
            b.ToTable("Batches", "Inv");
            b.Property(x => x.BatchNumber).IsRequired().HasMaxLength(50);
            b.Property(x => x.ProductionDate).HasMaxLength(10);
            b.Property(x => x.ExpiryDate).HasMaxLength(10);
            b.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            b.Property(x => x.PurchasePrice).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.ProductId, x.BatchNumber }).IsUnique();
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Inventory.Serial>(b =>
        {
            b.ToTable("Serials", "Inv");
            b.Property(x => x.SerialNumber).IsRequired().HasMaxLength(100);
            b.Property(x => x.Status).HasConversion(new SerialStatusToPersianConverter()).HasMaxLength(20);
            b.Property(x => x.PurchasePrice).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.ProductId, x.SerialNumber }).IsUnique();
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.CRM.CustomerAttachment>().ToTable("CustomerAttachments", "Crm");
        modelBuilder.Entity<Party>().ToTable("Parties", "Crm");   // طرف‌حساب یکپارچه
        modelBuilder.Entity<SalesInvoice>().ToTable("SalesInvoices", "Sal");
        modelBuilder.Entity<SalesInvoiceItem>().ToTable("SalesInvoiceItems", "Sal");
        // SalesInvoice: Status maps to the 'StatusCode' Persian column; InvoiceType is Persian NVARCHAR.
        modelBuilder.Entity<SalesInvoice>().Property(i => i.Status)
            .HasColumnName("StatusCode").HasConversion(new InvoiceStatusToPersianConverter());
        modelBuilder.Entity<SalesInvoice>().Property(i => i.InvoiceType)
            .HasConversion(new InvoiceTypeToPersianConverter());
        // Navigations not needed for now.
        modelBuilder.Entity<SalesInvoice>().Ignore(i => i.Payments);
        // The FK column is InvoiceId (not the convention 'SalesInvoiceId').
        modelBuilder.Entity<SalesInvoice>()
            .HasMany(i => i.Items).WithOne().HasForeignKey(it => it.InvoiceId);
        // Recurring invoices (P3): schema Sal.
        modelBuilder.Entity<RecurringInvoice>(b =>
        {
            b.ToTable("RecurringInvoices", "Sal");
            b.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.RecurringInvoiceId);
        });
        modelBuilder.Entity<RecurringInvoiceLine>(b =>
        {
            b.ToTable("RecurringInvoiceLines", "Sal");
            b.Property(l => l.Quantity).HasPrecision(18, 3);
            b.Property(l => l.UnitPrice).HasPrecision(18, 2);
            b.Property(l => l.TaxPct).HasPrecision(18, 2);
        });

        // Purchase invoices live in the Pur schema.
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice>().ToTable("PurchaseInvoices", "Pur");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseInvoiceItem>().ToTable("PurchaseInvoiceItems", "Pur");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice>()
            .HasMany(i => i.Items).WithOne().HasForeignKey(it => it.InvoiceId);
        // Purchase orders (P12): schema Pur.
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseOrder>(b =>
        {
            b.ToTable("PurchaseOrders", "Pur");
            b.HasMany(o => o.Items).WithOne().HasForeignKey(it => it.OrderId);
            b.Property(o => o.Total).HasPrecision(18, 2);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseOrderItem>(b =>
        {
            b.ToTable("PurchaseOrderItems", "Pur");
            b.Property(i => i.Quantity).HasPrecision(18, 3);
            b.Property(i => i.UnitPrice).HasPrecision(18, 2);
            b.Property(i => i.LineTotal).HasPrecision(18, 2);
        });

        // Security: users + audit trail.
        modelBuilder.Entity<SamaHesab.Domain.Entities.Security.User>(b =>
        {
            b.ToTable("Users", "Sec");
            b.Ignore("Avatar"); // VARBINARY column not needed by the app
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Security.AuditLog>(b =>
        {
            b.ToTable("AuditLogs", "Sec");
            b.HasKey(a => a.Id);
        });

        // Employee/Department در هسته می‌مانند (داده‌پایهٔ سازمانیِ مشترک — فروش/رستوران/گردشگری مصرفش می‌کنند).
        modelBuilder.Entity<Employee>().ToTable("Employees", "Hrm");
        modelBuilder.Entity<SamaHesab.Domain.Entities.HRM.Department>(b =>
        {
            b.ToTable("Departments", "Hrm");
            b.Ignore(d => d.CreatedAt);   // Hrm.Departments ستون‌های تاریخِ ممیزی ندارد
            b.Ignore(d => d.UpdatedAt);
        });
        // حقوق+حضوروغیاب (SalarySlip/PayrollSetting/Shift/Holiday/LeaveRequest/AttendanceRecord/Device/RawPunch)
        // به SamaHesab.Modules.HR منتقل شدند → نگاشتِ EFشان در HrModule.ConfigureModel است.
        // CR-X8 — تنظیماتِ شرکتیِ کلید-مقدار در DB.
        modelBuilder.Entity<SamaHesab.Domain.Entities.Settings.CompanySetting>().ToTable("CompanySettings", "Cfg");

        // ─── Tourism (TUR-C1-1): schema Tur ─────────────────────────────────────
        modelBuilder.Entity<Domain.Entities.Tourism.ProductGroup>().ToTable("ProductGroups", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.TourismProduct>().ToTable("Products", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.SupplierDeposit>().ToTable("SupplierDeposits", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.TourismSetting>().ToTable("Settings", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.CommissionRule>().ToTable("CommissionRules", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.SalesCommissionEntry>().ToTable("CommissionEntries", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.SupplierDailyReport>().ToTable("SupplierDailyReports", "Tur");
        modelBuilder.Entity<Domain.Entities.Tourism.TourismSale>(b =>
        {
            b.ToTable("Sales", "Tur");
            b.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SaleId);
        });
        modelBuilder.Entity<Domain.Entities.Tourism.TourismSaleLine>(b =>
        {
            b.ToTable("SaleLines", "Tur");
            b.HasMany(l => l.Passengers).WithOne().HasForeignKey(p => p.SaleLineId);
        });
        modelBuilder.Entity<Domain.Entities.Tourism.SalePassenger>().ToTable("SalePassengers", "Tur");

        // ─── Contracting → استخراج شد به SamaHesab.Modules.Contracting (فاز ۲). مپش از ContractingModule. ───

        // ─── Hotel / PMS → استخراج شد به SamaHesab.Modules.Hotel (فاز ۱). مپش از HotelModule.ConfigureModel می‌آید. ───

        // ─── Voucher Templates (productivity): schema Acc ───────────────────────
        modelBuilder.Entity<VoucherTemplate>(b =>
        {
            b.ToTable("VoucherTemplates", "Acc");
            b.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.TemplateId);
        });
        modelBuilder.Entity<VoucherTemplateLine>(b =>
        {
            b.ToTable("VoucherTemplateLines", "Acc");
            b.Property(l => l.Debit).HasPrecision(18, 2);
            b.Property(l => l.Credit).HasPrecision(18, 2);
        });
        modelBuilder.Entity<RecurringVoucher>().ToTable("RecurringVouchers", "Acc");

        // ─── امنیت/RBAC: نقش · مجوز نقش · نقش کاربر — schema Sec ─────────────────────
        modelBuilder.Entity<SamaHesab.Domain.Entities.Security.Role>(b =>
        {
            b.ToTable("Roles", "Sec");
            b.Property(x => x.Code).IsRequired().HasMaxLength(50);
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.HasMany(x => x.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Security.RolePermission>(b =>
        {
            b.ToTable("RolePermissions", "Sec");
            b.Property(x => x.PermissionCode).IsRequired().HasMaxLength(100);
            b.HasIndex(x => new { x.RoleId, x.PermissionCode }).IsUnique();
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Security.UserRole>(b =>
        {
            b.ToTable("UserRoles", "Sec");
            b.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        });

        // ─── ابعاد حسابداری (هستهٔ ERP): سال مالی · مرکز هزینه · پروژه — schema Acc ───
        modelBuilder.Entity<FiscalYear>(b =>
        {
            b.ToTable("FiscalYears", "Acc");
            b.Property(x => x.Title).IsRequired().HasMaxLength(50);
            b.Property(x => x.StartDate).IsRequired().HasMaxLength(10);
            b.Property(x => x.EndDate).IsRequired().HasMaxLength(10);
            b.HasIndex(x => new { x.CompanyId, x.Title }).IsUnique();
        });
        modelBuilder.Entity<CostCenter>(b =>
        {
            b.ToTable("CostCenters", "Acc");
            b.Property(x => x.Code).IsRequired().HasMaxLength(30);
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("Projects", "Acc");
            b.Property(x => x.Code).IsRequired().HasMaxLength(30);
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.Property(x => x.StartDate).HasMaxLength(10);
            b.Property(x => x.EndDate).HasMaxLength(10);
            b.Property(x => x.Budget).HasColumnType("decimal(18,2)");
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<SamaHesab.Domain.Entities.Settings.UserItemRef>().ToTable("UserItemRefs", "Cfg");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Documents.DocumentTemplate>(b =>   // فاز ۱۰ DT-2
        {
            b.ToTable("DocumentTemplates", "Cfg");
            b.Property(x => x.DocumentType).HasMaxLength(60);
            b.Property(x => x.Name).HasMaxLength(150);
            b.Property(x => x.PaperSize).HasMaxLength(20);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Settings.Branch>(b =>
        {
            b.ToTable("Branches", "Cfg");
            b.Property(x => x.Code).IsRequired().HasMaxLength(20);
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Ignore(x => x.Company);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.POS.CashShift>(b =>
        {
            b.ToTable("CashShifts", "Pos");
            foreach (var p in new[] { "OpeningFloat", "CashSales", "CardSales", "CountedCash", "ExpectedCash", "Variance" })
                b.Property(p).HasPrecision(18, 2);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.POS.HeldSale>(b =>
        {
            b.ToTable("HeldSales", "Pos");
            b.Property(h => h.Total).HasPrecision(18, 2);
        });
        // CRM(باشگاه/امتیاز) استخراج شد → نگاشتِ LoyaltyTransactions در CrmModule.ConfigureModel (Modules.CRM).

        // ─── Stock Count (انبارگردانی) — schema Inv ──────────────────────────────
        modelBuilder.Entity<StockCountSession>(b =>
        {
            b.ToTable("StockCountSessions", "Inv");
            b.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SessionId);
        });
        modelBuilder.Entity<StockCountLine>(b =>
        {
            b.ToTable("StockCountLines", "Inv");
            b.Ignore(l => l.Variance);   // محاسباتی
            b.Property(l => l.SystemQty).HasPrecision(18, 3);
            b.Property(l => l.CountedQty).HasPrecision(18, 3);
        });

        // ─── Restaurant → استخراج شد به SamaHesab.Modules.Restaurant (MOD-REST). مپش از RestaurantModule. ───

        // ─── 🆘 HC-2 — Support Center (schema Sup; enums به‌صورتِ INT) ───────────
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.BugReport>(b =>
        {
            b.ToTable("BugReports", "Sup");
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.ScreenName).HasMaxLength(120);
            b.Property(x => x.RemoteId).HasMaxLength(60);
            b.Property(x => x.AttachmentPath).HasMaxLength(400);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.FeatureRequest>(b =>
        {
            b.ToTable("FeatureRequests", "Sup");
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.RemoteId).HasMaxLength(60);
            b.Property(x => x.AttachmentPath).HasMaxLength(400);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.SupportTicket>(b =>
        {
            b.ToTable("SupportTickets", "Sup");
            b.Property(x => x.Subject).IsRequired().HasMaxLength(200);
            b.Property(x => x.RemoteId).HasMaxLength(60);
            b.HasMany(t => t.Messages).WithOne().HasForeignKey(m => m.TicketId);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.TicketMessage>(b =>
        {
            b.ToTable("TicketMessages", "Sup");
            b.Property(x => x.Author).HasMaxLength(120);
            b.Property(x => x.AttachmentPath).HasMaxLength(400);
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.KnowledgeArticle>(b =>
        {
            b.ToTable("KnowledgeArticles", "Sup");
            b.Property(x => x.RemoteId).IsRequired().HasMaxLength(60);
            b.Property(x => x.Title).IsRequired().HasMaxLength(250);
            b.Property(x => x.Kind).HasMaxLength(20);
            b.Property(x => x.Url).HasMaxLength(500);
            b.HasIndex(x => new { x.CompanyId, x.RemoteId }).IsUnique();
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.ReleaseNote>(b =>
        {
            b.ToTable("ReleaseNotes", "Sup");
            b.Property(x => x.RemoteId).IsRequired().HasMaxLength(60);
            b.Property(x => x.Version).IsRequired().HasMaxLength(40);
            b.HasIndex(x => new { x.CompanyId, x.RemoteId }).IsUnique();
        });
        modelBuilder.Entity<SamaHesab.Domain.Entities.Support.RemoteSupportSession>(b =>
        {
            b.ToTable("RemoteSupportSessions", "Sup");
            b.Property(x => x.Code).IsRequired().HasMaxLength(40);
            b.Property(x => x.RequestedBy).HasMaxLength(120);
            b.Property(x => x.LogPath).HasMaxLength(400);
            b.Property(x => x.ConnectId).HasMaxLength(80);   // HC-6b — شناسهٔ RustDesk
            b.Property(x => x.RemoteId).HasMaxLength(60);
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        // Cheque enums are stored as Persian NVARCHAR in the DB.
        modelBuilder.Entity<Cheque>().Property(c => c.Status)
            .HasConversion(new ChequeStatusToPersianConverter());
        modelBuilder.Entity<Cheque>().Property(c => c.ChequeType)
            .HasConversion(new ChequeTypeToPersianConverter());

        // The audit-by-user columns are not present in every table created by the
        // SQL scripts. They are not used by the UI, so ignore them everywhere to
        // avoid "Invalid column name 'CreatedByUserId'" at query time.
        // ── ماژولارسازی (G4): مدلِ موجودیتِ ماژول‌های نصب‌شده/فعال — *پیش از* حلقهٔ فیلترِ عمومی ثبت می‌شود
        //    تا موجودیتِ ماژول (AuditableEntity) خودکار فیلترِ شرکت + Ignoreِ ستون‌های audit را بگیرد.
        //    ماژولِ ثبت‌نشده (غیرفعال/حذف‌شده) → موجودیتش مپ نمی‌شود و هسته سالم می‌ماند.
        foreach (var module in _modules)
            module.ConfigureModel(modelBuilder);

        var applyTenant = typeof(ApplicationDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var et in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(SamaHesab.Domain.Common.AuditableEntity).IsAssignableFrom(et.ClrType))
            {
                var eb = modelBuilder.Entity(et.ClrType);
                // ستون CreatedByUserId در همهٔ جدول‌ها وجود ندارد؛ نادیده گرفته می‌شود.
                // استثنا: Acc.Vouchers این ستون را دارد (برای نمایش «کاربرِ» ثبت‌کننده در فهرست اسناد).
                if (et.ClrType != typeof(Voucher))
                    eb.Ignore(nameof(SamaHesab.Domain.Common.AuditableEntity.CreatedByUserId));
                eb.Ignore(nameof(SamaHesab.Domain.Common.AuditableEntity.UpdatedByUserId));

                // multi-tenant global query filter (scoped to current company)
                applyTenant.MakeGenericMethod(et.ClrType).Invoke(this, new object[] { modelBuilder });
            }
        }

        // فیلتر ترکیبیِ شرکت+شعبه روی موجودیت‌های شعبه‌ای — جایگزینِ فیلترِ فقط-شرکت بالا.
        ApplyTenantAndBranchFilter<Voucher>(modelBuilder);
        // MB-2 — گسترش به فروش/خرید (هماهنگی با C2). فقط برای کاربرِ بدونِ Security.AllBranches فعال می‌شود.
        ApplyTenantAndBranchFilter<Domain.Entities.Sales.SalesInvoice>(modelBuilder);
        ApplyTenantAndBranchFilter<Domain.Entities.Purchase.PurchaseInvoice>(modelBuilder);
        // TUR-C1-1 — فروشِ گردشگری شعبه‌ای است.
        ApplyTenantAndBranchFilter<Domain.Entities.Tourism.TourismSale>(modelBuilder);
        // PMS: رزروِ هتل به ماژولِ Hotel منتقل شد؛ فیلترِ شرکت (multi-tenant) از حلقهٔ عمومیِ بالا اعمال می‌شود.
        //   فیلترِ شعبه‌ایِ موجودیتِ ماژول = follow-upِ سخت‌سازیِ seam (فاز ۴). Hotel هنوز دادهٔ تولیدی ندارد.
        // MB-3 — جداسازیِ شعبهٔ انبار (هماهنگی با C2). فیلترِ nullable-aware: انبارِ بدونِ شعبه (null)
        // مشترک و برای همه دیده می‌شود؛ وگرنه فقط شعبهٔ کاربر. ادمین/AllBranches همه را می‌بیند.
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e =>
            (!_tenantFilterEnabled || e.CompanyId == _companyId)
            && (!_branchScopeEnabled || e.BranchId == null || e.BranchId == _branchId));

        // MB-3 (تکمیل) — موجودیت‌های شعبه‌ایِ انبار/POS: انبارگردانی، شیفتِ صندوق، فاکتورِ معلق.
        ApplyTenantAndBranchFilter<StockCountSession>(modelBuilder);
        ApplyTenantAndBranchFilter<SamaHesab.Domain.Entities.POS.CashShift>(modelBuilder);
        ApplyTenantAndBranchFilter<SamaHesab.Domain.Entities.POS.HeldSale>(modelBuilder);
        // کاردکس (StockTransaction) موجودیتِ AuditableEntity نیست و BranchId آن nullable است → فیلترِ سفارشی.
        modelBuilder.Entity<StockTransaction>().HasQueryFilter(e =>
            (!_tenantFilterEnabled || e.CompanyId == _companyId)
            && (!_branchScopeEnabled || e.BranchId == null || e.BranchId == _branchId));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events from tracked aggregates BEFORE saving.
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch AFTER a successful save so handlers see persisted state.
        if (_publisher is not null && domainEvents.Count > 0)
        {
            foreach (var e in entitiesWithEvents) e.ClearDomainEvents();
            foreach (var domainEvent in domainEvents)
            {
                var notification = Activator.CreateInstance(
                    typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType()), domainEvent);
                if (notification is INotification n)
                    await _publisher.Publish(n, cancellationToken);
            }
        }

        return result;
    }
}
