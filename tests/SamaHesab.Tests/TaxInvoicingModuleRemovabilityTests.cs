using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.TaxInvoicing;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// U-ACCT-2 (ماژولِ سامانهٔ مودیان) — اثباتِ removability، هم‌الگو با هتل/گردشگری/... : بدونِ ثبتِ
/// TaxInvoicingModule هسته سالم است و موجودیت‌هایِ مودیان اصلاً در مدل نیستند؛ با ثبت، در schemaی
/// اختصاصیِ Tax مپ می‌شوند.
/// </summary>
public class TaxInvoicingModuleRemovabilityTests
{
    private static ApplicationDbContext BuildContext(bool withModule)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPublisher());
        if (withModule) s.AddSingleton<IModule, TaxInvoicingModule>();
        return s.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }

    private sealed class StubPublisher : MediatR.IPublisher
    {
        public System.Threading.Tasks.Task Publish(object notification, System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken ct = default)
            where TNotification : MediatR.INotification => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public void Core_Works_Without_TaxInvoicing_Module_And_Its_Entities_Are_Not_Mapped()
    {
        using var ctx = BuildContext(withModule: false);
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TaxInvoicing.Domain.ElectronicInvoiceSubmission)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TaxInvoicing.Domain.TaxItemCode)));
    }

    [Fact]
    public void TaxInvoicing_Entities_Are_Mapped_To_Tax_Schema_When_Module_Installed()
    {
        using var ctx = BuildContext(withModule: true);
        var submission = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TaxInvoicing.Domain.ElectronicInvoiceSubmission));
        Assert.NotNull(submission);
        Assert.Equal("Tax", submission!.GetSchema());
        Assert.Equal("Submissions", submission.GetTableName());

        var itemCode = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TaxInvoicing.Domain.TaxItemCode));
        Assert.NotNull(itemCode);
        Assert.Equal("Tax", itemCode!.GetSchema());
        Assert.Equal("ItemCodes", itemCode.GetTableName());
    }
}
