namespace SamaHesab.Application.Workflow;

/// <summary>وضعیت‌های گردش‌کار تأیید یک سند (عمومی: سند حسابداری/فاکتور/سفارش خرید).</summary>
public enum ApprovalState
{
    Draft,             // پیش‌نویس
    PendingApproval,   // در انتظار تأیید
    Approved,          // تأییدشده
    Rejected           // ردشده
}

/// <summary>کنش‌های قابل‌اعمال روی گردش‌کار.</summary>
public enum ApprovalAction
{
    Submit,    // ارسال برای تأیید
    Approve,   // تأیید
    Reject,    // رد
    Reopen     // بازگشت به پیش‌نویس (برای اصلاح)
}

/// <summary>نتیجه‌ی یک گذار.</summary>
public record TransitionResult(bool Allowed, ApprovalState NewState, string? Error = null);

/// <summary>
/// موتور گردش‌کار تأیید — ماشین‌وضعیت خالص و تست‌پذیر، مستقل از نوع سند.
/// قواعد گذار:
///   Draft           --Submit-->  PendingApproval
///   PendingApproval --Approve--> Approved
///   PendingApproval --Reject-->  Rejected
///   Rejected        --Reopen-->  Draft
///   Approved        نهایی است (بدون گذار).
/// </summary>
public static class ApprovalWorkflow
{
    private static readonly Dictionary<(ApprovalState, ApprovalAction), ApprovalState> Map = new()
    {
        { (ApprovalState.Draft,           ApprovalAction.Submit),  ApprovalState.PendingApproval },
        { (ApprovalState.PendingApproval, ApprovalAction.Approve), ApprovalState.Approved },
        { (ApprovalState.PendingApproval, ApprovalAction.Reject),  ApprovalState.Rejected },
        { (ApprovalState.Rejected,        ApprovalAction.Reopen),  ApprovalState.Draft },
    };

    public static bool CanTransition(ApprovalState from, ApprovalAction action)
        => Map.ContainsKey((from, action));

    public static IEnumerable<ApprovalAction> AllowedActions(ApprovalState from)
        => Map.Keys.Where(k => k.Item1 == from).Select(k => k.Item2);

    public static bool IsFinal(ApprovalState state) => state == ApprovalState.Approved;

    public static TransitionResult Apply(ApprovalState from, ApprovalAction action)
        => Map.TryGetValue((from, action), out var to)
            ? new TransitionResult(true, to)
            : new TransitionResult(false, from, $"گذار نامعتبر: {action} از وضعیت {from}");
}
