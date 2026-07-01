using SamaHesab.Application.CRM.Commands;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// AUDIT-4 — همان اعتبارسنجیِ نامِ CreateCustomerCommand باید در مسیرِ ویرایش هم باشد
/// (قبلاً فقط ساخت داشت → نامِ نامعتبر مثلِ «؟» یا خالی می‌توانست با ویرایش ثبت بماند).
/// </summary>
public class UpdateCustomerValidatorTests
{
    private static UpdateCustomerCommand Make(string type, string? first, string? last, string? company, int id = 1)
        => new(id, type, first, last, company, null, null, null, null, null, null, null,
               0, 0, "خرده", 0, null, null, null, null, null,
               IsCustomerRole: true, IsSupplierRole: false, IsEmployeeRole: false, IsSalespersonRole: false);

    private readonly UpdateCustomerCommandValidator _v = new();

    [Fact]
    public void Rejects_Empty_Name_For_Real_Person()
        => Assert.False(_v.Validate(Make("حقیقی", "", "", null)).IsValid);

    [Fact]
    public void Rejects_QuestionMark_Name()
        // بایتِ 0x3F = «?»ِ ASCII (نه «؟»ِ فارسی) — همان چیزی که در AUDIT-4 در DB پیدا شد.
        => Assert.False(_v.Validate(Make("حقیقی", "??????", "", null)).IsValid);

    [Fact]
    public void Rejects_Empty_CompanyName_For_Legal_Person()
        => Assert.False(_v.Validate(Make("حقوقی", null, null, " ")).IsValid);

    [Fact]
    public void Accepts_Valid_Real_Person()
        => Assert.True(_v.Validate(Make("حقیقی", "علی", "رضایی", null)).IsValid);

    [Fact]
    public void Rejects_When_No_Role_Checked()
    {
        var cmd = new UpdateCustomerCommand(1, "حقیقی", "علی", "رضایی", null, null, null, null, null, null, null, null,
            0, 0, "خرده", 0, null, null, null, null, null,
            IsCustomerRole: false, IsSupplierRole: false, IsEmployeeRole: false, IsSalespersonRole: false);
        Assert.False(_v.Validate(cmd).IsValid);
    }
}
