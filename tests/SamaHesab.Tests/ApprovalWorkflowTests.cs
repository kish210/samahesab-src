using SamaHesab.Application.Workflow;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ماشین‌وضعیت گردش‌کار تأیید.</summary>
public class ApprovalWorkflowTests
{
    [Theory]
    [InlineData(ApprovalState.Draft, ApprovalAction.Submit, ApprovalState.PendingApproval)]
    [InlineData(ApprovalState.PendingApproval, ApprovalAction.Approve, ApprovalState.Approved)]
    [InlineData(ApprovalState.PendingApproval, ApprovalAction.Reject, ApprovalState.Rejected)]
    [InlineData(ApprovalState.Rejected, ApprovalAction.Reopen, ApprovalState.Draft)]
    public void Valid_Transitions(ApprovalState from, ApprovalAction action, ApprovalState expected)
    {
        var r = ApprovalWorkflow.Apply(from, action);
        Assert.True(r.Allowed);
        Assert.Equal(expected, r.NewState);
    }

    [Theory]
    [InlineData(ApprovalState.Draft, ApprovalAction.Approve)]      // نمی‌توان پیش‌نویس را مستقیم تأیید کرد
    [InlineData(ApprovalState.Approved, ApprovalAction.Reject)]    // تأییدشده نهایی است
    [InlineData(ApprovalState.Draft, ApprovalAction.Reopen)]
    public void Invalid_Transitions_Are_Blocked(ApprovalState from, ApprovalAction action)
    {
        var r = ApprovalWorkflow.Apply(from, action);
        Assert.False(r.Allowed);
        Assert.Equal(from, r.NewState);      // وضعیت تغییر نمی‌کند
        Assert.NotNull(r.Error);
    }

    [Fact]
    public void Approved_Is_Final()
    {
        Assert.True(ApprovalWorkflow.IsFinal(ApprovalState.Approved));
        Assert.Empty(ApprovalWorkflow.AllowedActions(ApprovalState.Approved));
    }

    [Fact]
    public void Pending_Allows_Approve_And_Reject()
    {
        var actions = ApprovalWorkflow.AllowedActions(ApprovalState.PendingApproval);
        Assert.Contains(ApprovalAction.Approve, actions);
        Assert.Contains(ApprovalAction.Reject, actions);
    }
}
