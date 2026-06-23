using SamaHesab.Application.CRM.Commands;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>رفعِ باگ (اشخاص→مشتریان): نام‌های نامعتبر/خالی نباید ثبت شوند.</summary>
public class CreateCustomerValidatorTests
{
    private static CreateCustomerCommand Make(string type, string? first, string? last, string? company, string code = "C1")
        => new(code, type, first, last, company, null, null, null, null, null, null, null,
               0, 0, "خرده", 0, null, null, null, null, null, null, null);

    private readonly CreateCustomerCommandValidator _v = new();

    [Fact]
    public void Rejects_Empty_Name_For_Real_Person()
        => Assert.False(_v.Validate(Make("حقیقی", "", "", null)).IsValid);

    [Fact]
    public void Rejects_Empty_CompanyName_For_Legal_Person()
        => Assert.False(_v.Validate(Make("حقوقی", null, null, " ")).IsValid);

    [Fact]
    public void Rejects_Empty_Code()
        => Assert.False(_v.Validate(Make("حقیقی", "علی", "رضایی", null, code: "")).IsValid);

    [Fact]
    public void Accepts_Valid_Real_Person()
        => Assert.True(_v.Validate(Make("حقیقی", "علی", "رضایی", null)).IsValid);

    [Fact]
    public void Accepts_Valid_Legal_Person()
        => Assert.True(_v.Validate(Make("حقوقی", null, null, "بازرگانی پارس")).IsValid);
}
