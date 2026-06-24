using System.Linq;
using SamaHesab.Infrastructure.Modules;
using SamaHesab.Modules.Abstractions;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ماژول‌های لِینِ pc (فاز ۳ ماژولارسازی) — HR و CRM پشتِ قراردادِ IModule.</summary>
public class C1ModulesTests
{
    [Fact]
    public void HrModule_Key_And_Metadata()
    {
        IModule hr = new HrModule();
        Assert.Equal("HR", hr.Key);
        Assert.Equal("منابع انسانی", hr.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(hr.Version));
    }

    [Fact]
    public void HrModule_Registers_Payroll_And_Attendance_Permissions()
    {
        var perms = new HrModule().GetPermissions();
        Assert.All(perms, p => Assert.Equal("HR", p.Module));
        Assert.Contains(perms, p => p.Feature == "Payroll" && p.Action == "Run");
        Assert.Contains(perms, p => p.Feature == "Attendance" && p.Action == "Manage");
        Assert.Contains(perms, p => p.Feature == "Leave" && p.Action == "Approve");
    }

    [Fact]
    public void HrModule_Migration_Scripts_Cover_Payroll_And_Attendance_Schema()
    {
        var scripts = new HrModule().GetMigrationScripts();
        Assert.Contains("39_PayrollFullSchema.sql", scripts);
        Assert.Contains("41_AttendanceSchema.sql", scripts);
        Assert.Contains("45_AttendanceDevices.sql", scripts);
    }

    [Fact]
    public void CrmModule_Is_Loyalty_Only()
    {
        IModule crm = new CrmModule();
        Assert.Equal("CRM", crm.Key);
        var perms = crm.GetPermissions();
        Assert.All(perms, p => Assert.Equal("Loyalty", p.Feature));   // اشخاص هسته است؛ فقط باشگاه ماژول
        Assert.Contains(perms, p => p.Action == "Manage");
    }

    [Fact]
    public void Module_Keys_Match_ModuleService_Optional_Keys()
    {
        // کلیدها باید با کلیدِ ماژولِ ModuleService یکی باشند (HR/CRM) تا گیتِ فعال/غیرفعال درست بخورد.
        Assert.Equal("HR", new HrModule().Key);
        Assert.Equal("CRM", new CrmModule().Key);
    }
}
