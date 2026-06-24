using SamaHesab.Application.Common.Behaviors;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>BUG-9 — payloadِ لاگِ حسابرسی باید فارسی را خوانا ذخیره کند (نه \uXXXX).</summary>
public class AuditPayloadTests
{
    [Fact]
    public void Persian_Stored_Readable_Not_Unicode_Escaped()
    {
        var json = AuditPayload.Serialize(new { AccountName = "حساب‌های دریافتنی", Note = "اصلاحِ سند" });

        Assert.Contains("حساب‌های دریافتنی", json);
        Assert.Contains("اصلاحِ سند", json);
        Assert.DoesNotContain("\\u", json);   // هیچ گریزِ یونیکدی نباید باشد
    }

    [Fact]
    public void Ascii_And_Numbers_Unaffected()
    {
        var json = AuditPayload.Serialize(new { Code = "1-04-001", Amount = 5000 });
        Assert.Contains("1-04-001", json);
        Assert.Contains("5000", json);
    }
}
