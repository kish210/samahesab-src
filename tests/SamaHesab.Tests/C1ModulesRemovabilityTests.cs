using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// اثباتِ removability برای ماژول‌های لِینِ pc (CRM/HR/Attendance): بدونِ ثبتِ ماژول، موجودیتش در مدلِ
/// EF نیست ولی هسته سالم می‌ماند (Account + Employee/Department که هسته‌اند). با ثبتِ ماژول، مپ می‌شود.
/// مدل بدونِ اتصالِ DB ساخته می‌شود.
/// </summary>
public class C1ModulesRemovabilityTests
{
    private static ApplicationDbContext BuildContext(params IModule[] modules)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPublisher());
        foreach (var m in modules) s.AddSingleton<IModule>(m);
        return s.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }

    private sealed class StubPublisher : MediatR.IPublisher
    {
        public System.Threading.Tasks.Task Publish(object n, System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task Publish<T>(T n, System.Threading.CancellationToken ct = default)
            where T : MediatR.INotification => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public void Core_Intact_When_No_C1_Modules()
    {
        using var ctx = BuildContext();   // هیچ ماژولی نصب نیست
        // هسته سالم: حساب + Employee/Department (داده‌پایهٔ مشترکِ هسته) مپ‌اند.
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.Employee)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.Department)));
        // موجودیتِ ماژول‌های نصب‌نشده در مدل نیست:
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.CRM.Domain.LoyaltyTransaction)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.SalarySlip)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.AttendanceRecord)));
    }

    [Fact]
    public void Crm_Mapped_Only_When_Installed()
    {
        using var ctx = BuildContext(new SamaHesab.Modules.CRM.CrmModule());
        var t = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.CRM.Domain.LoyaltyTransaction));
        Assert.NotNull(t);
        Assert.Equal("Crm", t!.GetSchema());
        Assert.Equal("LoyaltyTransactions", t.GetTableName());
    }

    [Fact]
    public void Hr_Mapped_Only_When_Installed()
    {
        using var ctx = BuildContext(new SamaHesab.Modules.HR.HrModule());
        var slip = ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.SalarySlip));
        Assert.NotNull(slip);
        Assert.Equal("Hrm", slip!.GetSchema());
        // حضور به HR وابسته نیست → نصبِ HR، موجودیتِ Attendance را نمی‌آورد.
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.AttendanceRecord)));
    }

    [Fact]
    public void Attendance_Mapped_Only_When_Installed()
    {
        using var ctx = BuildContext(new SamaHesab.Modules.Attendance.AttendanceModule());
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.AttendanceRecord)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.Shift)));
        // حقوق نصب نشده → SalarySlip نباید باشد (دو ماژولِ مستقل).
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.SalarySlip)));
    }

    [Fact]
    public void All_Three_Coexist_When_All_Installed()
    {
        using var ctx = BuildContext(
            new SamaHesab.Modules.CRM.CrmModule(),
            new SamaHesab.Modules.HR.HrModule(),
            new SamaHesab.Modules.Attendance.AttendanceModule());
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.CRM.Domain.LoyaltyTransaction)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.SalarySlip)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.HRM.AttendanceRecord)));
    }
}
