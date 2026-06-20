using SamaHesab.Domain.Entities.CRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>UX-CRM-EDIT — به‌روزرسانیِ مشخصاتِ مشتری روی موجودیتِ Party (نه ساختِ تکراری).</summary>
public class PartyEditTests
{
    [Fact]
    public void EditCore_Updates_Names_And_Type()
    {
        var p = Party.Create(1, "C100", "حقیقی", firstName: "علی", lastName: "رضایی", isCustomer: true);

        p.EditCore("حقوقی", firstName: null, lastName: null, companyName: "پارس تجارت",
            postalCode: "1234567890", contactPerson: "خانم احمدی", visitor: "ویزیتور ۱");

        Assert.Equal("حقوقی", p.PartyType);
        Assert.Equal("پارس تجارت", p.CompanyName);
        Assert.Equal("پارس تجارت", p.FullName);   // نوع حقوقی → نامِ شرکت
        Assert.Equal("1234567890", p.PostalCode);
        Assert.Equal("خانم احمدی", p.ContactPerson);
        Assert.Equal("ویزیتور ۱", p.Visitor);
    }

    [Fact]
    public void Profile_And_CreditTerms_Update_In_Place()
    {
        var p = Party.Create(1, "C101", "حقیقی", firstName: "سارا", lastName: "محمدی", isCustomer: true);
        var id = p.Id;

        p.UpdateProfile("0012345678", "09120000000", "02100000000", "a@b.c", "تهران", "تهران", "خ آزادی");
        p.SetCreditTerms(creditLimit: 5_000_000, creditDays: 30, priceLevel: "عمده", discount: 5);

        Assert.Equal(id, p.Id);                    // همان موجودیت (نه تکراری)
        Assert.Equal("09120000000", p.Mobile);
        Assert.Equal("تهران", p.City);
        Assert.Equal(5_000_000, p.CreditLimit);
        Assert.Equal(30, p.CreditDays);
        Assert.Equal("عمده", p.PriceLevel);
        Assert.Equal(5, p.Discount);
    }
}
