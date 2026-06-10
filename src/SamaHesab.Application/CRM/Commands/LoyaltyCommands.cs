using MediatR;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Commands;

// ── کسب امتیاز (مثلاً پس از فروش) ─────────────────────────────────────────────
public record AwardLoyaltyPointsCommand(int CustomerId, decimal PurchaseAmount, string Reason)
    : IRequest<Result<int>>;

public class AwardLoyaltyPointsCommandHandler : IRequestHandler<AwardLoyaltyPointsCommand, Result<int>>
{
    private readonly IRepository<LoyaltyTransaction> _loyalty; private readonly IUnitOfWork _uow;
    public AwardLoyaltyPointsCommandHandler(IRepository<LoyaltyTransaction> loyalty, IUnitOfWork uow)
    { _loyalty = loyalty; _uow = uow; }

    public async Task<Result<int>> Handle(AwardLoyaltyPointsCommand req, CancellationToken ct)
    {
        var points = LoyaltyPolicy.EarnedPoints(req.PurchaseAmount);
        if (points <= 0) return Result<int>.Success(0);   // مبلغ کم‌تر از یک امتیاز
        try
        {
            var tx = LoyaltyTransaction.Earn(req.CustomerId, points, req.Reason);
            await _loyalty.AddAsync(tx, ct); await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(points);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── استفاده از امتیاز ─────────────────────────────────────────────────────────
public record RedeemLoyaltyPointsCommand(int CustomerId, int Points, string Reason) : IRequest<Result>;

public class RedeemLoyaltyPointsCommandHandler : IRequestHandler<RedeemLoyaltyPointsCommand, Result>
{
    private readonly IRepository<LoyaltyTransaction> _loyalty; private readonly IUnitOfWork _uow;
    public RedeemLoyaltyPointsCommandHandler(IRepository<LoyaltyTransaction> loyalty, IUnitOfWork uow)
    { _loyalty = loyalty; _uow = uow; }

    public async Task<Result> Handle(RedeemLoyaltyPointsCommand req, CancellationToken ct)
    {
        var txns = await _loyalty.FindAsync(t => t.CustomerId == req.CustomerId, ct);
        var balance = txns.Sum(t => t.Points);
        if (!LoyaltyPolicy.CanRedeem(balance, req.Points))
            return Result.Failure($"امتیاز کافی نیست (موجودی: {balance}).");
        try
        {
            var tx = LoyaltyTransaction.Redeem(req.CustomerId, req.Points, req.Reason);
            await _loyalty.AddAsync(tx, ct); await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}
