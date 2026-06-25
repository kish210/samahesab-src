using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Automation.Commands;
using SamaHesab.Application.Common.Interfaces;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>P2 — کامندِ ارسالِ پیامکیِ یادآورها (شمارشِ موفق/ناموفق).</summary>
public class SendOverdueRemindersCommandTests
{
    /// <summary>SMS فِیکِ کنترل‌شده: شماره‌هایی که در FailFor باشند false برمی‌گردانند.</summary>
    private sealed class FakeSms : ISmsService
    {
        public HashSet<string> FailFor { get; } = new();
        public List<string> Sent { get; } = new();
        public Task<bool> SendAsync(string mobile, string message, CancellationToken ct = default)
        {
            if (FailFor.Contains(mobile)) return Task.FromResult(false);
            Sent.Add(mobile);
            return Task.FromResult(true);
        }
        public Task<bool> SendBulkAsync(IEnumerable<string> mobiles, string message, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> SendTemplateAsync(string mobile, string templateCode, Dictionary<string, string> parameters, CancellationToken ct = default) => Task.FromResult(true);
        public Task<decimal> GetCreditAsync(CancellationToken ct = default) => Task.FromResult(0m);
    }

    private static Reminder R(string mobile) =>
        new(mobile, "علی", ReminderKind.OverdueDebt, 1_000_000, "متنِ یادآور");

    [Fact]
    public async Task Sends_All_And_Counts_Successes()
    {
        var sms = new FakeSms();
        var handler = new SendOverdueRemindersCommandHandler(sms);
        var reminders = new List<Reminder> { R("09120000001"), R("09120000002") };

        var res = await handler.Handle(new SendOverdueRemindersCommand(reminders), CancellationToken.None);

        Assert.Equal(2, res.Sent);
        Assert.Equal(0, res.Failed);
        Assert.Equal(2, res.Total);
        Assert.Equal(2, sms.Sent.Count);
    }

    [Fact]
    public async Task Counts_Failures_Without_Throwing()
    {
        var sms = new FakeSms();
        sms.FailFor.Add("09120000002");
        var handler = new SendOverdueRemindersCommandHandler(sms);
        var reminders = new List<Reminder> { R("09120000001"), R("09120000002"), R("") };

        var res = await handler.Handle(new SendOverdueRemindersCommand(reminders), CancellationToken.None);

        Assert.Equal(1, res.Sent);    // فقط شمارهٔ اول موفق
        Assert.Equal(2, res.Failed);  // یکی fail سرویس + یکی موبایلِ خالی
    }
}
