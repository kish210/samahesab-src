using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel;

/// <summary>
/// ماژولِ هتل / PMS (پایلوتِ استخراج، فاز ۱). کدِ هتل اکنون در این اسمبلیِ مستقل است؛
/// هسته دیگر موجودیتِ هتل را hard-code نمی‌کند و فقط از طریقِ این `IModule` آن را می‌شناسد.
/// schema: Htl · مهاجرت: 47_Pms.sql.
/// </summary>
public sealed class HotelModule : IModule
{
    public string Key => "Hotel";
    public string DisplayName => "هتل / اقامتگاه (PMS)";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services)
    {
        // U-WEB-HOTEL — لایهٔ Application (اتاق/نوعِ اتاق/رزرو/فولیو) نو اضافه شد.
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(HotelModule).Assembly));
        // ریپازیتوریِ اختصاصی با Include — IRepository<T>.GetByIdAsync عمومی ناوبری‌ها را
        // بارگذاری نمی‌کند (باگِ کشف‌شده در تستِ زنده: Rooms/Charges/Payments همیشه خالی می‌ماند).
        services.AddScoped<IReservationRepository, Infrastructure.ReservationRepository>();
        services.AddScoped<IFolioRepository, Infrastructure.FolioRepository>();
    }

    /// <summary>مپِ EFِ موجودیت‌های هتل (G4) — منتقل‌شده از ApplicationDbContext. schema Htl.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoomType>().ToTable("RoomTypes", "Htl");
        modelBuilder.Entity<Room>().ToTable("Rooms", "Htl");
        modelBuilder.Entity<RatePlan>().ToTable("RatePlans", "Htl");
        modelBuilder.Entity<Reservation>(b =>
        {
            b.ToTable("Reservations", "Htl");
            b.HasMany(r => r.Rooms).WithOne().HasForeignKey(rr => rr.ReservationId);
        });
        modelBuilder.Entity<ReservationRoom>().ToTable("ReservationRooms", "Htl");
        modelBuilder.Entity<RoomNightBlock>().ToTable("RoomNightBlocks", "Htl");
        modelBuilder.Entity<Folio>(b =>
        {
            b.ToTable("Folios", "Htl");
            b.Ignore(f => f.Balance);
            b.Ignore(f => f.IsChargeable);
            b.HasMany(f => f.Charges).WithOne().HasForeignKey(c => c.FolioId);
            b.HasMany(f => f.Payments).WithOne().HasForeignKey(p => p.FolioId);
        });
        modelBuilder.Entity<FolioCharge>().ToTable("FolioCharges", "Htl");
        modelBuilder.Entity<FolioPayment>().ToTable("FolioPayments", "Htl");
        modelBuilder.Entity<Deposit>(b =>
        {
            b.ToTable("Deposits", "Htl");
            b.Ignore(d => d.Remaining);
        });
        modelBuilder.Entity<HousekeepingTask>().ToTable("HousekeepingTasks", "Htl");
        modelBuilder.Entity<MaintenanceTicket>().ToTable("MaintenanceTickets", "Htl");
        modelBuilder.Entity<NightAuditRun>().ToTable("NightAuditRuns", "Htl");
        modelBuilder.Entity<PmsSettings>().ToTable("Settings", "Htl");
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();          // هنوز صفحهٔ WPF ندارد
    public IReadOnlyList<ModulePermission> GetPermissions() => System.Array.Empty<ModulePermission>();
    public IReadOnlyList<string> GetMigrationScripts() => new[] { "47_Pms.sql" };
}
