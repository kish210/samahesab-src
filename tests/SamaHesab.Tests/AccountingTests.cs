using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>یکپارچگی حسابداری دوطرفه — قلب صحت ERP.</summary>
public class AccountingTests
{
    private static Voucher NewVoucher() =>
        Voucher.Create(companyId: 1, branchId: 1, fiscalYearId: 1,
            voucherNumber: "1", voucherDate: "1404/01/01", voucherTypeId: 1, description: "تست");

    [Fact]
    public void Voucher_IsBalanced_When_Debit_Equals_Credit()
    {
        var v = NewVoucher();
        v.AddItem(VoucherItem.Create(0, 1, accountId: 10, debit: 1000, credit: 0));
        v.AddItem(VoucherItem.Create(0, 2, accountId: 20, debit: 0, credit: 1000));

        Assert.True(v.IsBalanced());
        Assert.Equal(1000, v.TotalDebit);
        Assert.Equal(1000, v.TotalCredit);
    }

    [Fact]
    public void Voucher_NotBalanced_When_Sums_Differ()
    {
        var v = NewVoucher();
        v.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0));
        v.AddItem(VoucherItem.Create(0, 2, 20, 0, 900));

        Assert.False(v.IsBalanced());
    }

    [Fact]
    public void Post_Throws_When_Not_Balanced()
    {
        var v = NewVoucher();
        v.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0));
        v.AddItem(VoucherItem.Create(0, 2, 20, 0, 500));

        Assert.Throws<InvalidOperationException>(() => v.Post(userId: 1));
        Assert.Equal(VoucherStatus.Draft, v.Status);
    }

    [Fact]
    public void Post_Succeeds_And_Raises_Event_When_Balanced()
    {
        var v = NewVoucher();
        v.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0));
        v.AddItem(VoucherItem.Create(0, 2, 20, 0, 1000));

        v.Post(userId: 7);

        Assert.Equal(VoucherStatus.Posted, v.Status);
        Assert.Contains(v.DomainEvents, e => e is VoucherPostedEvent);
    }

    [Fact]
    public void AddItem_Throws_After_Posted()
    {
        var v = NewVoucher();
        v.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0));
        v.AddItem(VoucherItem.Create(0, 2, 20, 0, 1000));
        v.Post(1);

        Assert.Throws<InvalidOperationException>(() => v.AddItem(VoucherItem.Create(0, 3, 30, 50, 0)));
    }

    [Fact]
    public void VoucherItem_Throws_When_Both_Debit_And_Credit()
        => Assert.Throws<ArgumentException>(() => VoucherItem.Create(0, 1, 10, debit: 100, credit: 100));

    [Fact]
    public void VoucherItem_Throws_When_Negative()
        => Assert.Throws<ArgumentException>(() => VoucherItem.Create(0, 1, 10, debit: -100, credit: 0));

    [Fact]
    public void Posted_Voucher_CanReverse_Until_Reversed()
    {
        var v = NewVoucher();
        v.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0));
        v.AddItem(VoucherItem.Create(0, 2, 20, 0, 1000));
        v.Post(1);

        Assert.True(v.CanReverse());
        v.MarkAsReversed();
        Assert.False(v.CanReverse());
    }

    [Fact]
    public void Reversal_With_Swapped_Amounts_Is_Balanced_And_Linked()
    {
        // original
        var orig = NewVoucher();
        orig.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0));
        orig.AddItem(VoucherItem.Create(0, 2, 20, 0, 1000));
        orig.Post(1);

        // reversal = swap debit/credit
        var rev = Voucher.Create(1, 1, 1, "2", "1404/01/02", 1, "برگشت");
        foreach (var i in orig.Items)
            rev.AddItem(VoucherItem.Create(0, i.RowNumber, i.AccountId, debit: i.Credit, credit: i.Debit));
        rev.SetAsReversalOf(orig.Id);
        rev.Post(1);

        Assert.True(rev.IsBalanced());
        Assert.Equal(orig.Id, rev.ReversedFromId);
        Assert.Equal(orig.TotalDebit, rev.TotalCredit);   // 1000 debit ⇄ 1000 credit
        Assert.Equal(orig.TotalCredit, rev.TotalDebit);
    }
}
