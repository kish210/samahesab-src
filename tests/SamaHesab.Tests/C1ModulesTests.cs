using System.Linq;
using SamaHesab.Modules.HR;                // HrModule (حقوق)
using SamaHesab.Modules.CRM;               // CrmModule (باشگاه)
using SamaHesab.Modules.Attendance;        // AttendanceModule (حضوروغیاب)
using SamaHesab.Modules.Abstractions;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ماژول‌های لِینِ pc (فاز ۳) — HR(حقوق) · CRM(باشگاه) · Attendance(حضور) پشتِ IModule.</summary>
public class C1ModulesTests
{
    [Fact]
    public void HrModule_Key_And_Metadata()
    {
        IModule hr = new HrModule();
        Assert.Equal("HR", hr.Key);
        Assert.Equal("حقوق و دستمزد", hr.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(hr.Version));
    }

    [Fact]
    public void HrModule_Is_Payroll_Only_Not_Attendance()
    {
        var perms = new HrModule().GetPermissions();
        Assert.All(perms, p => Assert.Equal("HR", p.Module));
        Assert.Contains(perms, p => p.Feature == "Payroll" && p.Action == "Run");
        // حضور به ماژولِ جداگانه رفت ⇒ HR دیگر مجوزِ Attendance/Leave ندارد.
        Assert.DoesNotContain(perms, p => p.Feature == "Attendance");
        Assert.DoesNotContain(perms, p => p.Feature == "Leave");
    }

    [Fact]
    public void HrModule_Migration_Scripts_Cover_Payroll_Only()
    {
        var scripts = new HrModule().GetMigrationScripts();
        Assert.Contains("39_PayrollFullSchema.sql", scripts);
        Assert.DoesNotContain("41_AttendanceSchema.sql", scripts);   // مالِ ماژولِ Attendance
    }

    [Fact]
    public void AttendanceModule_Owns_Attendance_Permissions_And_Migrations()
    {
        IModule att = new AttendanceModule();
        Assert.Equal("Attendance", att.Key);
        var perms = att.GetPermissions();
        Assert.Contains(perms, p => p.Feature == "Attendance" && p.Action == "Manage");
        Assert.Contains(perms, p => p.Feature == "Leave" && p.Action == "Approve");
        var scripts = att.GetMigrationScripts();
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
