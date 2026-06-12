using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SamaHesab.Application.Common.Events;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Entities.Restaurant;

namespace SamaHesab.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    // ── Multi-tenancy (Phase 0): scope every AuditableEntity to the current company ──
    // مقدارها در سازنده از کاربر جاری خوانده می‌شوند؛ EF این ارجاع‌ها را per-query پارامتری می‌کند.
    private readonly int _companyId;
    private readonly bool _tenantFilterEnabled;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPublisher publisher,
        ICurrentUserService? currentUser = null)
        : base(options)
    {
        _publisher = publisher;
        _companyId = currentUser?.CompanyId ?? 0;
        // وقتی کاربری احراز نشده (seeding/ورود/سرویس‌های پس‌زمینه) فیلتر غیرفعال است تا چیزی نشکند.
        _tenantFilterEnabled = _companyId > 0;
    }

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
    public DbSet<SamaHesab.Domain.Entities.Settings.UserItemRef> UserItemRefs { get; set; }
    public DbSet<SamaHesab.Domain.Entities.POS.CashShift> CashShifts { get; set; }
    public DbSet<SamaHesab.Domain.Entities.POS.HeldSale> HeldSales { get; set; }
    public DbSet<SamaHesab.Domain.Entities.CRM.LoyaltyTransaction> LoyaltyTransactions { get; set; }
    public DbSet<StockCountSession> StockCountSessions { get; set; }
    public DbSet<StockCountLine> StockCountLines { get; set; }

    // Inventory
    public DbSet<Product> Products { get; set; }
    public DbSet<StockItem> StockItems { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }

    // CRM
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Supplier> Suppliers { get; set; }

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

    // Restaurant (v2)
    public DbSet<Hall> Halls { get; set; }
    public DbSet<DiningTable> DiningTables { get; set; }
    public DbSet<RestaurantOrder> RestaurantOrders { get; set; }
    public DbSet<RestaurantOrderItem> RestaurantOrderItems { get; set; }
    public DbSet<KitchenTicket> KitchenTickets { get; set; }

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
        modelBuilder.Entity<Customer>().ToTable("Customers", "Crm");
        modelBuilder.Entity<Supplier>().ToTable("Suppliers", "Crm");
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

        modelBuilder.Entity<Employee>().ToTable("Employees", "Hrm");
        // Avoid cascading the HR detail tables into the model for now.
        modelBuilder.Entity<Employee>().Ignore(e => e.AttendanceRecords);
        modelBuilder.Entity<Employee>().Ignore(e => e.SalarySlips);

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
        modelBuilder.Entity<SamaHesab.Domain.Entities.CRM.LoyaltyTransaction>().ToTable("LoyaltyTransactions", "Crm");

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

        // ─── Restaurant (v2): schema Rst, enums stored as INT ───────────────────
        modelBuilder.Entity<Hall>(b =>
        {
            b.ToTable("Halls", "Rst");
            b.Ignore(h => h.Tables);   // tables are queried directly by HallId
        });
        modelBuilder.Entity<DiningTable>().ToTable("DiningTables", "Rst");
        modelBuilder.Entity<KitchenTicket>().ToTable("KitchenTickets", "Rst");
        modelBuilder.Entity<RestaurantOrder>(b =>
        {
            b.ToTable("RestaurantOrders", "Rst");
            b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
            foreach (var p in new[] { "SubTotal", "Discount", "ServiceCharge", "Tax", "Tip", "GrandTotal", "PaidAmount" })
                b.Property(p).HasPrecision(18, 2);
        });
        modelBuilder.Entity<RestaurantOrderItem>(b =>
        {
            b.ToTable("RestaurantOrderItems", "Rst");
            b.Property(i => i.Quantity).HasPrecision(18, 3);
            foreach (var p in new[] { "UnitPrice", "DiscountAmount", "LineTotal" })
                b.Property(p).HasPrecision(18, 2);
        });

        // Cheque enums are stored as Persian NVARCHAR in the DB.
        modelBuilder.Entity<Cheque>().Property(c => c.Status)
            .HasConversion(new ChequeStatusToPersianConverter());
        modelBuilder.Entity<Cheque>().Property(c => c.ChequeType)
            .HasConversion(new ChequeTypeToPersianConverter());

        // The audit-by-user columns are not present in every table created by the
        // SQL scripts. They are not used by the UI, so ignore them everywhere to
        // avoid "Invalid column name 'CreatedByUserId'" at query time.
        var applyTenant = typeof(ApplicationDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var et in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(SamaHesab.Domain.Common.AuditableEntity).IsAssignableFrom(et.ClrType))
            {
                var eb = modelBuilder.Entity(et.ClrType);
                eb.Ignore(nameof(SamaHesab.Domain.Common.AuditableEntity.CreatedByUserId));
                eb.Ignore(nameof(SamaHesab.Domain.Common.AuditableEntity.UpdatedByUserId));

                // multi-tenant global query filter (scoped to current company)
                applyTenant.MakeGenericMethod(et.ClrType).Invoke(this, new object[] { modelBuilder });
            }
        }
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
