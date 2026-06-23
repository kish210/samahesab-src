using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Tourism.Commands;

/// <summary>
/// TUR-C2-6 — ساخت/ویرایشِ محصول یا خدمتِ گردشگری. Id=0 → ساخت، وگرنه ویرایش.
/// (قطعهٔ گم‌شده تا کاربر بتواند از داخلِ برنامه محصولِ گردشگری تعریف کند.)
/// </summary>
public record SaveTourismProductCommand(
    int Id, string Name, int SupplierPartyId, decimal PurchasePrice, decimal DefaultSalePrice,
    int? ProductGroupId, bool RequiresPassengerList, bool Active = true) : IRequest<Result<int>>;

public class SaveTourismProductCommandValidator : AbstractValidator<SaveTourismProductCommand>
{
    public SaveTourismProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("نامِ محصول الزامی است.")
            .MinimumLength(2).WithMessage("نامِ محصول دستِ‌کم ۲ نویسه باشد.");
        RuleFor(x => x.SupplierPartyId).GreaterThan(0).WithMessage("تأمین‌کننده را انتخاب کنید.");
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultSalePrice).GreaterThanOrEqualTo(0);
    }
}

public class SaveTourismProductCommandHandler : IRequestHandler<SaveTourismProductCommand, Result<int>>
{
    private readonly IRepository<TourismProduct> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveTourismProductCommandHandler(IRepository<TourismProduct> products, IUnitOfWork uow, ICurrentUserService user)
    { _products = products; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveTourismProductCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            if (req.Id > 0)
            {
                var existing = await _products.FindSingleAsync(p => p.Id == req.Id && p.CompanyId == companyId, ct);
                if (existing is null) return Result<int>.Failure("محصول یافت نشد.");
                existing.Update(req.Name, req.SupplierPartyId, req.PurchasePrice, req.DefaultSalePrice,
                    req.ProductGroupId, req.RequiresPassengerList, req.Active);
                _products.Update(existing);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(existing.Id);
            }

            var np = TourismProduct.Create(companyId, req.Name, req.SupplierPartyId,
                req.PurchasePrice, req.DefaultSalePrice, req.ProductGroupId, req.RequiresPassengerList);
            await _products.AddAsync(np, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(np.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

/// <summary>غیرفعال‌سازیِ (حذفِ نرمِ) محصولِ گردشگری.</summary>
public record DeleteTourismProductCommand(int Id) : IRequest<Result>;

public class DeleteTourismProductCommandHandler : IRequestHandler<DeleteTourismProductCommand, Result>
{
    private readonly IRepository<TourismProduct> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public DeleteTourismProductCommandHandler(IRepository<TourismProduct> products, IUnitOfWork uow, ICurrentUserService user)
    { _products = products; _uow = uow; _user = user; }

    public async Task<Result> Handle(DeleteTourismProductCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var p = await _products.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct);
        if (p is null) return Result.Failure("محصول یافت نشد.");
        p.Update(p.Name, p.SupplierPartyId, p.PurchasePrice, p.DefaultSalePrice, p.ProductGroupId, p.RequiresPassengerList, active: false);
        _products.Update(p);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
