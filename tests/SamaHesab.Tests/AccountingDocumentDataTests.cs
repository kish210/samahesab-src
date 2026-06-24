using System.Collections.Generic;
using System.Linq;
using SamaHesab.Application.Documents;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>DT-6 — دادهٔ قالبِ اسنادِ مالی (سند حسابداری/چک) + رندر با موتورِ قالب.</summary>
public class AccountingDocumentDataTests
{
    private static Voucher SampleVoucher()
    {
        var v = Voucher.Create(1, 1, 1, "S-100", "1405/04/03", 9, "بابتِ آزمون", "REF-7");
        v.AddItem(VoucherItem.Create(0, 1, 101, 5_000_000, 0, "بدهکارِ صندوق"));
        v.AddItem(VoucherItem.Create(0, 2, 202, 0, 5_000_000, "بستانکارِ بانک"));
        return v;
    }

    [Fact]
    public void Voucher_Fields_And_Rows_Populated()
    {
        var names = new Dictionary<int, string?> { [101] = "صندوق", [202] = "بانک ملت" };
        var data = AccountingDocumentData.Voucher(SampleVoucher(), id => names.GetValueOrDefault(id));

        Assert.Equal("S-100", data.Fields["VoucherNumber"]);
        Assert.Equal("1405/04/03", data.Fields["VoucherDate"]);
        Assert.Equal("5,000,000", data.Fields["TotalDebit"]);
        Assert.Equal("5,000,000", data.Fields["TotalCredit"]);
        Assert.Equal(2, data.Rows.Count);
        Assert.Equal("صندوق", data.Rows[0]["AccountName"]);
        Assert.Equal("5,000,000", data.Rows[0]["Debit"]);
        Assert.Equal("", data.Rows[0]["Credit"]);            // صفر → خالی
        Assert.Equal("بانک ملت", data.Rows[1]["AccountName"]);
    }

    [Fact]
    public void Voucher_Unknown_Account_Falls_Back_To_Id()
    {
        var data = AccountingDocumentData.Voucher(SampleVoucher(), _ => null);
        Assert.Equal("#101", data.Rows[0]["AccountName"]);
    }

    [Fact]
    public void Voucher_Renders_Through_Engine()
    {
        var names = new Dictionary<int, string?> { [101] = "صندوق", [202] = "بانک ملت" };
        var data = AccountingDocumentData.Voucher(SampleVoucher(), id => names.GetValueOrDefault(id));
        var html = DocumentTemplateEngine.Render(
            "سند {VoucherNumber} تاریخ {VoucherDate}\n[[ROWS]]{#}- {AccountName}: {Debit}{Credit}\n[[/ROWS]]جمع {TotalDebit}", data);

        Assert.Contains("سند S-100 تاریخ 1405/04/03", html);
        Assert.Contains("صندوق: 5,000,000", html);
        Assert.Contains("بانک ملت", html);
        Assert.Contains("جمع 5,000,000", html);
        Assert.Contains("۱-", html);   // شمارهٔ ردیفِ فارسی {#}
    }

    [Fact]
    public void Cheque_Fields_Populated()
    {
        var c = Cheque.Create(1, 1, ChequeType.Received, "7788", "ملت", 9_000_000, "1405/05/10", "علی", "بابتِ فاکتور");
        var data = AccountingDocumentData.Cheque(c, "شرکتِ آلفا");

        Assert.Equal("7788", data.Fields["ChequeNumber"]);
        Assert.Equal("9,000,000", data.Fields["Amount"]);
        Assert.Equal("دریافتی", data.Fields["Type"]);
        Assert.Equal("در جریان", data.Fields["Status"]);
        Assert.Equal("شرکتِ آلفا", data.Fields["PartyName"]);
        Assert.Equal("ملت", data.Fields["BankName"]);
    }

    [Fact]
    public void Cheque_Paid_Type_Label()
    {
        var c = Cheque.Create(1, 1, ChequeType.Paid, "9", "صادرات", 100, "1405/06/01");
        Assert.Equal("پرداختی", AccountingDocumentData.Cheque(c, null).Fields["Type"]);
    }
}
