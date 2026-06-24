using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Modules.Attendance;

/// <summary>
/// ماژولِ حضوروغیاب (فاز ۳، استخراجِ مستقل از حقوق). تردد/شیفت/تقویم/مرخصی/دستگاه در این اسمبلی است.
/// حقوق از طریقِ `IAttendanceAggregateProvider` (اینترفیسِ هسته) کارکردِ ماه را می‌گیرد ⇒ بی‌وابستگیِ
/// مستقیمِ حقوق↔حضور. اپلیکیشنِ مستقلِ hozur.exe هم همین ماژول را میزبانی می‌کند. schema: Hrm.
/// </summary>
public sealed class AttendanceModule : IModule
{
    public string Key => "Attendance";
    public string DisplayName => "حضور و غیاب";
    public string Version => "1.0.0";

    /// <summary>هندلرهای MediatRِ حضور + تأمین‌کنندهٔ تجمیعِ حقوق + چک‌کنندهٔ وابستگیِ کارمند (تردد).</summary>
    public void RegisterServices(IServiceCollection services)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(AttendanceModule).Assembly));
        services.AddScoped<IAttendanceAggregateProvider, Application.AttendanceAggregateProvider>();
        services.AddScoped<IEmployeeDependencyChecker, Application.AttendanceEmployeeDependencyChecker>();
    }

    /// <summary>مپِ EFِ موجودیت‌های حضور (G4). فیلترِ شرکت/Ignoreِ audit را حلقهٔ عمومیِ DbContext اعمال می‌کند.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttendanceRecord>().ToTable("AttendanceRecords", "Hrm");
        modelBuilder.Entity<Shift>().ToTable("Shifts", "Hrm");
        modelBuilder.Entity<Holiday>().ToTable("Holidays", "Hrm");
        modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests", "Hrm");
        modelBuilder.Entity<AttendanceDevice>().ToTable("Devices", "Hrm");
        modelBuilder.Entity<RawPunch>().ToTable("RawPunches", "Hrm");
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("Attendance", "Attendance", "Manage", "ثبت/ویرایشِ تردد + شیفت/تقویم/دستگاه"),
        new ModulePermission("Attendance", "Attendance", "View",   "مشاهدهٔ گزارشِ حضوروغیاب"),
        new ModulePermission("Attendance", "Leave",      "Approve","تأیید/ردِ مرخصی"),
    };

    public IReadOnlyList<string> GetMigrationScripts() => new[]
    {
        "41_AttendanceSchema.sql",
        "45_AttendanceDevices.sql",
    };
}
