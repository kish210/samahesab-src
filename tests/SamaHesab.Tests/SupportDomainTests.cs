using System;
using System.Linq;
using SamaHesab.Application.Support;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>🆘 HC-2 — منطقِ دامنهٔ مرکزِ پشتیبانی (گزارش/تیکت/ریموت) + اعتبارِ کلید-API.</summary>
public class SupportDomainTests
{
    // ── BugReport ──
    [Fact]
    public void Bug_Create_Defaults_New_And_Local()
    {
        var b = BugReport.Create(1, "کرش هنگامِ چاپ", "هنگامِ چاپِ فاکتور برنامه بسته می‌شود",
            BugSeverity.High, SupportCategory.Sales, steps: "۱) فاکتور را باز کن ۲) چاپ بزن");
        Assert.Equal(SupportStatus.New, b.Status);
        Assert.Equal(SyncState.Local, b.Sync);
        Assert.Equal(BugSeverity.High, b.Severity);
        Assert.Null(b.RemoteId);
    }

    [Fact]
    public void Bug_MarkSynced_Sets_RemoteId_And_State()
    {
        var b = BugReport.Create(1, "t", "d", BugSeverity.Low, SupportCategory.System);
        b.MarkQueued();
        Assert.Equal(SyncState.Queued, b.Sync);
        b.MarkSynced("WP-1024");
        Assert.Equal(SyncState.Synced, b.Sync);
        Assert.Equal("WP-1024", b.RemoteId);
        Assert.NotNull(b.SyncedAt);
    }

    [Theory]
    [InlineData("", "d")]
    [InlineData("t", "")]
    public void Bug_Create_Rejects_Empty(string title, string desc)
        => Assert.Throws<ArgumentException>(() =>
            BugReport.Create(1, title, desc, BugSeverity.Low, SupportCategory.Other));

    // ── SupportTicket ──
    [Fact]
    public void Ticket_AddMessage_Tracks_Conversation_And_Activity()
    {
        var t = SupportTicket.Create(1, "سؤال دربارهٔ مالیات", "نرخِ مالیات کجا تنظیم می‌شود؟", SupportCategory.Accounting);
        var before = t.LastActivityAt;
        t.AddMessage("کاربر", "لطفاً راهنمایی کنید", fromSupport: false);
        t.AddMessage("پشتیبانی", "از مسیرِ تنظیمات…", fromSupport: true);
        Assert.Equal(2, t.Messages.Count);
        Assert.True(t.Messages.Last().FromSupport);
        Assert.True(t.LastActivityAt >= before);
    }

    [Fact]
    public void Ticket_Status_Cycle()
    {
        var t = SupportTicket.Create(1, "s", "b", SupportCategory.Pos);
        t.ChangeStatus(SupportStatus.InProgress);
        Assert.Equal(SupportStatus.InProgress, t.Status);
        t.ChangeStatus(SupportStatus.Resolved);
        Assert.Equal(SupportStatus.Resolved, t.Status);
    }

    // ── RemoteSupportSession ──
    [Fact]
    public void Remote_Open_Activate_End_Lifecycle()
    {
        var s = RemoteSupportSession.Open(1, "SH-7F3A-21", "مدیر", "بررسیِ گزارش", validMinutes: 30);
        Assert.Equal(RemoteSessionStatus.Pending, s.Status);
        Assert.True(s.IsValid);
        s.Activate();
        Assert.Equal(RemoteSessionStatus.Active, s.Status);
        Assert.NotNull(s.StartedAt);
        s.End(@"C:\logs\session.txt");
        Assert.Equal(RemoteSessionStatus.Ended, s.Status);
        Assert.Equal(@"C:\logs\session.txt", s.LogPath);
        Assert.False(s.IsValid);
    }

    [Fact]
    public void Remote_Activate_Twice_Throws()
    {
        var s = RemoteSupportSession.Open(1, "SH-1", null, null);
        s.Activate();
        Assert.Throws<InvalidOperationException>(() => s.Activate());
    }

    // ── کلید-API ──
    [Theory]
    [InlineData("https://kishwifi.com", "C1", "KEY", true)]
    [InlineData("", "C1", "KEY", false)]
    [InlineData("https://kishwifi.com", "", "KEY", false)]
    [InlineData("https://kishwifi.com", "C1", "", false)]
    public void SupportApiOptions_IsValid(string baseUrl, string customer, string key, bool expected)
        => Assert.Equal(expected, new SupportApiOptions(baseUrl, customer, key, "LIC").IsValid);
}
