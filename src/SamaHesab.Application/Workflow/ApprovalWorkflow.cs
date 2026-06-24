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

    // ── P6 — تأییدِ چندسطحی + تفکیکِ وظایف (SoD) — افزایشی روی همان ماشینِ وضعیت ──

    /// <summary>نتیجهٔ گذارِ سطح‌دار: وضعیت + سطحِ تأییدِ جاری (۱-مبنا؛ ۰=پیش‌نویس/ردشده).</summary>
    public record LeveledTransition(bool Allowed, ApprovalState NewState, int NewLevel, string? Error = null);

    /// <summary>
    /// گذارِ تأییدِ چندسطحی: یک سند پیش از «تأییدِ نهایی» باید <paramref name="totalLevels"/> بار تأیید شود
    /// (سلسله‌مراتبِ تأیید). <paramref name="enforceSoD"/>=true ⇒ ثبت‌کننده نمی‌تواند تأیید/رد کند.
    ///   Draft/Rejected? → Submit → PendingApproval (سطح ۱)
    ///   PendingApproval → Approve → اگر سطح<کل: همان وضعیت، سطح+۱ ؛ وگرنه Approved
    ///   PendingApproval → Reject → Rejected ؛ Rejected → Reopen → Draft (سطح ۰)
    /// </summary>
    public static LeveledTransition ApplyLeveled(
        ApprovalState from, ApprovalAction action, int currentLevel, int totalLevels,
        int actorUserId, int? submitterUserId, bool enforceSoD = false)
    {
        var total = totalLevels < 1 ? 1 : totalLevels;

        if (action is ApprovalAction.Approve or ApprovalAction.Reject
            && enforceSoD && submitterUserId is int sub && sub == actorUserId)
            return new(false, from, currentLevel, "تفکیکِ وظایف: ثبت‌کنندهٔ سند نمی‌تواند آن را تأیید/رد کند.");

        if (action == ApprovalAction.Approve && from == ApprovalState.PendingApproval)
            return currentLevel >= total
                ? new(true, ApprovalState.Approved, currentLevel)
                : new(true, ApprovalState.PendingApproval, currentLevel + 1);

        var basic = Apply(from, action);
        if (!basic.Allowed) return new(false, from, currentLevel, basic.Error);
        var newLevel = basic.NewState switch
        {
            ApprovalState.PendingApproval => 1,   // Submit → سطحِ ۱
            ApprovalState.Draft => 0,             // Reopen → پیش‌نویس
            _ => currentLevel                     // Rejected/Approved سطح را نگه می‌دارد
        };
        return new(true, basic.NewState, newLevel);
    }
}
