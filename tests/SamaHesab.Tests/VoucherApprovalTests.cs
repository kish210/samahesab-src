using System;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>T22 — گردش‌کارِ تأییدِ سند روی موجودیتِ Voucher + گیتِ Post.</summary>
public class VoucherApprovalTests
{
    private static Voucher BalancedDraft()
    {
        var v = Voucher.Create(1, 1, 1, "1", "1403/03/15", 9, "آزمایش");
        v.AddItem(VoucherItem.Create(0, 1, 10, 1000, 0, "بد"));
        v.AddItem(VoucherItem.Create(0, 2, 11, 0, 1000, "بس"));
        return v;
    }

    [Fact]
    public void Submit_Moves_Draft_To_PendingApproval()
    {
        var v = BalancedDraft();
        v.SubmitForApproval();
        Assert.Equal(VoucherApprovalStatus.PendingApproval, v.ApprovalStatus);
    }

    [Fact]
    public void Pending_Voucher_Cannot_Be_Posted()
    {
        var v = BalancedDraft();
        v.SubmitForApproval();
        var ex = Assert.Throws<InvalidOperationException>(() => v.Post(1));
        Assert.Contains("تأیید", ex.Message);
        Assert.Equal(VoucherStatus.Draft, v.Status);   // قطعی نشد
    }

    [Fact]
    public void Approved_Voucher_Can_Be_Posted()
    {
        var v = BalancedDraft();
        v.SubmitForApproval();
        v.ApproveBy(7);
        Assert.Equal(VoucherApprovalStatus.Approved, v.ApprovalStatus);

        v.Post(7);   // نباید استثنا بدهد
        Assert.Equal(VoucherStatus.Posted, v.Status);
    }

    [Fact]
    public void Rejected_Blocks_Post_Until_Reopened()
    {
        var v = BalancedDraft();
        v.SubmitForApproval();
        v.RejectApproval();
        Assert.Throws<InvalidOperationException>(() => v.Post(1));

        v.ReopenApproval();                     // → خارج از گردش‌کار (null)
        Assert.Null(v.ApprovalStatus);
        v.Post(1);                              // حالا قابلِ قطعی است
        Assert.Equal(VoucherStatus.Posted, v.Status);
    }

    [Fact]
    public void Voucher_Outside_Workflow_Posts_Normally()
    {
        var v = BalancedDraft();                // ApprovalStatus == null
        Assert.Null(v.ApprovalStatus);
        v.Post(1);                              // سازگارِ عقب‌رو
        Assert.Equal(VoucherStatus.Posted, v.Status);
    }

    [Fact]
    public void Approve_Without_Pending_Throws()
    {
        var v = BalancedDraft();
        Assert.Throws<InvalidOperationException>(() => v.ApproveBy(1));   // هنوز ارسال نشده
    }
}
