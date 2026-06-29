using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TourismItinerary.Domain;

namespace SamaHesab.Modules.TourismItinerary.Application.Itinerary.Commands;

// ───────────────────────── محصولِ اقامتی ─────────────────────────

/// <summary>ساخت/ویرایشِ محصولِ اقامتی. Id=0 → ساخت، وگرنه ویرایش.</summary>
public record SaveItineraryProductCommand(
    int Id, string Name, decimal SalePrice, decimal Cost, int Capacity,
    int? SupplierPartyId = null, bool Active = true) : IRequest<Result<int>>;

public class SaveItineraryProductCommandValidator : AbstractValidator<SaveItineraryProductCommand>
{
    public SaveItineraryProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("نامِ محصول الزامی است.")
            .MinimumLength(2).WithMessage("نامِ محصول دستِ‌کم ۲ نویسه باشد.");
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
    }
}

public class SaveItineraryProductCommandHandler : IRequestHandler<SaveItineraryProductCommand, Result<int>>
{
    private readonly IRepository<ItineraryProduct> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveItineraryProductCommandHandler(IRepository<ItineraryProduct> products, IUnitOfWork uow, ICurrentUserService user)
    { _products = products; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveItineraryProductCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            if (req.Id > 0)
            {
                var existing = await _products.FindSingleAsync(p => p.Id == req.Id && p.CompanyId == companyId, ct);
                if (existing is null) return Result<int>.Failure("محصول یافت نشد.");
                existing.Update(req.Name, req.SalePrice, req.Cost, req.Capacity, req.SupplierPartyId, req.Active, _user.UserId);
                _products.Update(existing);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(existing.Id);
            }

            var np = ItineraryProduct.Create(companyId, req.Name, req.SalePrice, req.Cost, req.Capacity, req.SupplierPartyId);
            await _products.AddAsync(np, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(np.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

/// <summary>حذفِ نرمِ محصولِ اقامتی (غیرفعال‌سازی — دادهٔ تاریخی حفظ می‌شود).</summary>
public record DeleteItineraryProductCommand(int Id) : IRequest<Result>;

public class DeleteItineraryProductCommandHandler : IRequestHandler<DeleteItineraryProductCommand, Result>
{
    private readonly IRepository<ItineraryProduct> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public DeleteItineraryProductCommandHandler(IRepository<ItineraryProduct> products, IUnitOfWork uow, ICurrentUserService user)
    { _products = products; _uow = uow; _user = user; }

    public async Task<Result> Handle(DeleteItineraryProductCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var p = await _products.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct);
        if (p is null) return Result.Failure("محصول یافت نشد.");
        p.Update(p.Name, p.SalePrice, p.Cost, p.Capacity, p.SupplierPartyId, active: false, _user.UserId);
        _products.Update(p);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ───────────────────────── سانسِ زمانیِ محصول ─────────────────────────

/// <summary>ساخت/ویرایشِ سانسِ زمانیِ یک محصول. Id=0 → ساخت.</summary>
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
    private readonly IRepository<ItineraryProduct> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveProductSessionCommandHandler(IRepository<ProductSession> sessions, IRepository<ItineraryProduct> products,
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
