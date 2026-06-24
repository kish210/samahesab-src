using SamaHesab.Application.Workflow;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>P6 — تأییدِ چندسطحی + تفکیکِ وظایف (SoD) روی ApprovalWorkflow.ApplyLeveled.</summary>
public class ApprovalLeveledTests
{
    [Fact]
    public void Submit_Sets_Level_One()
    {
        var r = ApprovalWorkflow.ApplyLeveled(ApprovalState.Draft, ApprovalAction.Submit, 0, 2, actorUserId: 7, submitterUserId: null);
        Assert.True(r.Allowed);
        Assert.Equal(ApprovalState.PendingApproval, r.NewState);
        Assert.Equal(1, r.NewLevel);
    }

    [Fact]
    public void Single_Level_Approve_Finalizes()
    {
        var r = ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Approve, 1, 1, 9, 7);
        Assert.Equal(ApprovalState.Approved, r.NewState);
    }

    [Fact]
    public void Two_Level_Approve_Advances_Then_Finalizes()
    {
        var r1 = ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Approve, 1, 2, 9, 7);
        Assert.Equal(ApprovalState.PendingApproval, r1.NewState);
        Assert.Equal(2, r1.NewLevel);

        var r2 = ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Approve, 2, 2, 11, 7);
        Assert.Equal(ApprovalState.Approved, r2.NewState);
    }

    [Fact]
    public void SoD_Blocks_Submitter_Approving_Or_Rejecting()
    {
        var ap = ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Approve, 1, 1, actorUserId: 7, submitterUserId: 7, enforceSoD: true);
        Assert.False(ap.Allowed);
        var rj = ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Reject, 1, 1, 7, 7, enforceSoD: true);
        Assert.False(rj.Allowed);
    }

    [Fact]
    public void SoD_Off_Allows_Self_Approve()
        => Assert.True(ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Approve, 1, 1, 7, 7, enforceSoD: false).Allowed);

    [Fact]
    public void Reject_Is_Allowed_By_Different_User()
        => Assert.Equal(ApprovalState.Rejected,
            ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Reject, 1, 2, 9, 7, enforceSoD: true).NewState);

    [Fact]
    public void Reopen_Resets_Level_To_Zero()
    {
        var r = ApprovalWorkflow.ApplyLeveled(ApprovalState.Rejected, ApprovalAction.Reopen, 1, 2, 7, 7);
        Assert.Equal(ApprovalState.Draft, r.NewState);
        Assert.Equal(0, r.NewLevel);
    }

    [Fact]
    public void Invalid_Transition_Denied()
        => Assert.False(ApprovalWorkflow.ApplyLeveled(ApprovalState.Draft, ApprovalAction.Approve, 0, 1, 9, null).Allowed);

    [Fact]
    public void TotalLevels_Clamped_To_One()
    {
        // totalLevels=0 → مثلِ تک‌سطحی رفتار کند (تأیید نهایی).
        var r = ApprovalWorkflow.ApplyLeveled(ApprovalState.PendingApproval, ApprovalAction.Approve, 1, 0, 9, 7);
        Assert.Equal(ApprovalState.Approved, r.NewState);
    }
}
