using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Tourism.Domain;

namespace SamaHesab.Modules.Tourism.Application.Itinerary.Commands;

// ───────────────────────── سانسِ زمانیِ محصولِ گردشگری ─────────────────────────
//  (محصولات یکپارچه شدند: همان TourismProduct؛ سانس‌ها زمان‌بندیِ سفرِ آن محصول‌اند.)

/// <summary>ساخت/ویرایشِ سانسِ زمانیِ یک محصولِ گردشگری. Id=0 → ساخت.</summary>
public record SaveProductSessionCommand(
    int Id, int ProductId, string Label, int StartMinute, int EndMinute, int Capacity, bool Active = true)
    : IRequest<Result<int>>;

public class SaveProductSessionCommandValidator : AbstractValidator<SaveProductSessionCommand>
{
    public SaveProductSessionCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("محصول را انتخاب کنید.");
        RuleFor(x => x.Label).NotEmpty().WithMessage("برچسبِ سانس الزامی است.");
        RuleFor(x => x.EndMinute).GreaterThan(x => x.StartMinute).WithMessage("پایانِ سانس باید پس از شروع باشد.");
    }
}

public class SaveProductSessionCommandHandler : IRequestHandler<SaveProductSessionCommand, Result<int>>
{
    private readonly IRepository<ProductSession> _sessions;
    private readonly IRepository<TourismProduct> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveProductSessionCommandHandler(IRepository<ProductSession> sessions, IRepository<TourismProduct> products,
        IUnitOfWork uow, ICurrentUserService user)
    { _sessions = sessions; _products = products; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveProductSessionCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            var product = await _products.FindSingleAsync(p => p.Id == req.ProductId && p.CompanyId == companyId, ct);
            if (product is null) return Result<int>.Failure("محصولِ سانس یافت نشد.");

            if (req.Id > 0)
            {
                var existing = await _sessions.FindSingleAsync(s => s.Id == req.Id && s.CompanyId == companyId, ct);
                if (existing is null) return Result<int>.Failure("سانس یافت نشد.");
                existing.Update(req.Label, req.StartMinute, req.EndMinute, req.Capacity, req.Active, _user.UserId);
                _sessions.Update(existing);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(existing.Id);
            }

            var ns = ProductSession.Create(companyId, req.ProductId, req.Label, req.StartMinute, req.EndMinute, req.Capacity);
            await _sessions.AddAsync(ns, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(ns.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

/// <summary>حذفِ سانس.</summary>
public record DeleteProductSessionCommand(int Id) : IRequest<Result>;

public class DeleteProductSessionCommandHandler : IRequestHandler<DeleteProductSessionCommand, Result>
{
    private readonly IRepository<ProductSession> _sessions;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public DeleteProductSessionCommandHandler(IRepository<ProductSession> sessions, IUnitOfWork uow, ICurrentUserService user)
    { _sessions = sessions; _uow = uow; _user = user; }

    public async Task<Result> Handle(DeleteProductSessionCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var s = await _sessions.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct);
        if (s is null) return Result.Failure("سانس یافت نشد.");
        _sessions.Remove(s);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
