using SamaHesab.Application.Licensing;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-LIC-MODULE — نگاشتِ Tier→ماژول‌هایِ مجاز (پیش‌تر اصلاً وجود نداشت).</summary>
public class ModuleEntitlementsTests
{
    [Fact]
    public void Trial_Allows_Every_Module()
    {
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Trial, ModuleEntitlements.Pos));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Trial, ModuleEntitlements.Hr));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Trial, ModuleEntitlements.TaxInvoicing));
    }

    [Fact]
    public void Starter_Allows_No_Optional_Module()
    {
        Assert.False(ModuleEntitlements.IsAllowed(LicenseTier.Starter, ModuleEntitlements.Pos));
        Assert.False(ModuleEntitlements.IsAllowed(LicenseTier.Starter, ModuleEntitlements.Crm));
        Assert.False(ModuleEntitlements.IsAllowed(LicenseTier.Starter, ModuleEntitlements.Hr));
    }

    [Fact]
    public void Professional_Allows_Pos_Restaurant_Crm_But_Not_Hr_Or_Tourism()
    {
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Professional, ModuleEntitlements.Pos));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Professional, ModuleEntitlements.Restaurant));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Professional, ModuleEntitlements.Crm));
        Assert.False(ModuleEntitlements.IsAllowed(LicenseTier.Professional, ModuleEntitlements.Hr));
        Assert.False(ModuleEntitlements.IsAllowed(LicenseTier.Professional, ModuleEntitlements.Tourism));
    }

    [Fact]
    public void Enterprise_Allows_Every_Optional_Module()
    {
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.Pos));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.Hr));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.Tourism));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.Hotel));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.Contracting));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.Support));
        Assert.True(ModuleEntitlements.IsAllowed(LicenseTier.Enterprise, ModuleEntitlements.TaxInvoicing));
    }
}
