using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Infrastructure.Data.Configurations.Accounting;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.VoucherNumber).IsRequired().HasMaxLength(20);
        builder.Property(v => v.VoucherDate).IsRequired().HasMaxLength(10);
        builder.Property(v => v.Description).HasMaxLength(1000);
        builder.Property(v => v.Reference).HasMaxLength(100);
        builder.Property(v => v.TotalDebit).HasColumnType("decimal(18,2)");
        builder.Property(v => v.TotalCredit).HasColumnType("decimal(18,2)");
        builder.Property(v => v.ExchangeRate).HasColumnType("decimal(18,4)").HasDefaultValue(1m);
        builder.Property(v => v.Status).HasConversion<byte>().HasDefaultValue(VoucherStatus.Draft);
        builder.Property(v => v.CreatedAt).HasDefaultValueSql("GETDATE()");

        builder.HasMany(v => v.Items)
               .WithOne(i => i.Voucher)
               .HasForeignKey(i => i.VoucherId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.CompanyId, v.FiscalYearId, v.VoucherNumber }).IsUnique();
        builder.HasIndex(v => v.VoucherDate);
        builder.HasIndex(v => v.Status);
    }
}

public class VoucherItemConfiguration : IEntityTypeConfiguration<VoucherItem>
{
    public void Configure(EntityTypeBuilder<VoucherItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.Debit).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(i => i.Credit).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(i => i.ExchangeRate).HasColumnType("decimal(18,4)").HasDefaultValue(1m);

        builder.HasOne(i => i.Account)
               .WithMany(a => a.VoucherItems)
               .HasForeignKey(i => i.AccountId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.Nature)
               .HasConversion(new SamaHesab.Infrastructure.Data.AccountNatureToPersianConverter())
               .HasMaxLength(10);
        builder.Property(a => a.Level).HasConversion<byte>();
        builder.Property(a => a.AccountType).HasMaxLength(50);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("GETDATE()");

        builder.HasIndex(a => new { a.CompanyId, a.Code }).IsUnique();

        builder.HasMany(a => a.Children)
               .WithOne(a => a.Parent)
               .HasForeignKey(a => a.ParentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
