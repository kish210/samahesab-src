using Microsoft.EntityFrameworkCore;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.HRM;

namespace SamaHesab.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // Accounting
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<VoucherItem> VoucherItems { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Cheque> Cheques { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }

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

    // Purchase
    public DbSet<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice> PurchaseInvoices { get; set; }
    public DbSet<SamaHesab.Domain.Entities.Purchase.PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }

    // HRM
    public DbSet<Employee> Employees { get; set; }

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
        // Purchase invoices live in the Pur schema.
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice>().ToTable("PurchaseInvoices", "Pur");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseInvoiceItem>().ToTable("PurchaseInvoiceItems", "Pur");
        modelBuilder.Entity<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice>()
            .HasMany(i => i.Items).WithOne().HasForeignKey(it => it.InvoiceId);

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

        // Cheque enums are stored as Persian NVARCHAR in the DB.
        modelBuilder.Entity<Cheque>().Property(c => c.Status)
            .HasConversion(new ChequeStatusToPersianConverter());
        modelBuilder.Entity<Cheque>().Property(c => c.ChequeType)
            .HasConversion(new ChequeTypeToPersianConverter());

        // The audit-by-user columns are not present in every table created by the
        // SQL scripts. They are not used by the UI, so ignore them everywhere to
        // avoid "Invalid column name 'CreatedByUserId'" at query time.
        foreach (var et in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(SamaHesab.Domain.Common.AuditableEntity).IsAssignableFrom(et.ClrType))
            {
                var eb = modelBuilder.Entity(et.ClrType);
                eb.Ignore(nameof(SamaHesab.Domain.Common.AuditableEntity.CreatedByUserId));
                eb.Ignore(nameof(SamaHesab.Domain.Common.AuditableEntity.UpdatedByUserId));
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Handle domain events, audit, etc.
        return await base.SaveChangesAsync(cancellationToken);
    }
}
