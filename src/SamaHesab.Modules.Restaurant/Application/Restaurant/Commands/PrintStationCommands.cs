using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Restaurant.Domain;

namespace SamaHesab.Modules.Restaurant.Application.Commands;

// ───────────────────────── ایستگاهِ چاپ (فیش‌پرینتر) ─────────────────────────

/// <summary>ساخت/ویرایشِ ایستگاهِ چاپ. Id=0 → ساخت. تعیینِ پیش‌فرض، پیش‌فرضِ قبلی را برمی‌دارد.</summary>
public record SavePrintStationCommand(int Id, string Name, string? PrinterName, bool IsDefault, bool Active = true)
    : IRequest<Result<int>>;

public class SavePrintStationCommandValidator : AbstractValidator<SavePrintStationCommand>
{
    public SavePrintStationCommandValidator()
        => RuleFor(x => x.Name).NotEmpty().WithMessage("نامِ ایستگاه الزامی است.");
}

public class SavePrintStationCommandHandler : IRequestHandler<SavePrintStationCommand, Result<int>>
{
    private readonly IRepository<PrintStation> _stations;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SavePrintStationCommandHandler(IRepository<PrintStation> stations, IUnitOfWork uow, ICurrentUserService user)
    { _stations = stations; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SavePrintStationCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;

            // فقط یک ایستگاهِ پیش‌فرض: اگر این یکی پیش‌فرض می‌شود، بقیه را غیرپیش‌فرض کن.
            if (req.IsDefault)
            {
                var others = await _stations.FindAsync(s => s.CompanyId == companyId && s.IsDefault && s.Id != req.Id, ct);
                foreach (var o in others) { o.ClearDefault(); _stations.Update(o); }
            }

            if (req.Id > 0)
            {
                var ex = await _stations.FindSingleAsync(s => s.Id == req.Id && s.CompanyId == companyId, ct);
                if (ex is null) return Result<int>.Failure("ایستگاه یافت نشد.");
                ex.Update(req.Name, req.PrinterName, req.IsDefault, req.Active, _user.UserId);
                _stations.Update(ex);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(ex.Id);
            }

            var ns = PrintStation.Create(companyId, req.Name, req.PrinterName, req.IsDefault);
            await _stations.AddAsync(ns, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(ns.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

/// <summary>حذفِ ایستگاهِ چاپ + پاک‌کردنِ نگاشت‌های کالاهای آن.</summary>
public record DeletePrintStationCommand(int Id) : IRequest<Result>;

public class DeletePrintStationCommandHandler : IRequestHandler<DeletePrintStationCommand, Result>
{
    private readonly IRepository<PrintStation> _stations;
    private readonly IRepository<ProductStationMap> _maps;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public DeletePrintStationCommandHandler(IRepository<PrintStation> stations, IRepository<ProductStationMap> maps,
        IUnitOfWork uow, ICurrentUserService user)
    { _stations = stations; _maps = maps; _uow = uow; _user = user; }

    public async Task<Result> Handle(DeletePrintStationCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var s = await _stations.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct);
        if (s is null) return Result.Failure("ایستگاه یافت نشد.");
        var maps = await _maps.FindAsync(m => m.CompanyId == companyId && m.StationId == req.Id, ct);
        if (maps.Count > 0) _maps.RemoveRange(maps);
        _stations.Remove(s);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ───────────────────────── نگاشتِ کالا → ایستگاه ─────────────────────────

/// <summary>تعیینِ ایستگاهِ یک کالا (StationId=0 → حذفِ نگاشت = بازگشت به ایستگاهِ پیش‌فرض).</summary>
public record SetProductStationCommand(int ProductId, int StationId) : IRequest<Result>;

public class SetProductStationCommandHandler : IRequestHandler<SetProductStationCommand, Result>
{
    private readonly IRepository<ProductStationMap> _maps;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SetProductStationCommandHandler(IRepository<ProductStationMap> maps, IUnitOfWork uow, ICurrentUserService user)
    { _maps = maps; _uow = uow; _user = user; }

    public async Task<Result> Handle(SetProductStationCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            var existing = await _maps.FindSingleAsync(m => m.CompanyId == companyId && m.ProductId == req.ProductId, ct);

            if (req.StationId <= 0)   // حذفِ نگاشت
            {
                if (existing is not null) { _maps.Remove(existing); await _uow.SaveChangesAsync(ct); }
                return Result.Success();
            }

            if (existing is null)
                await _maps.AddAsync(ProductStationMap.Create(companyId, req.ProductId, req.StationId), ct);
            else { existing.Reassign(req.StationId, _user.UserId); _maps.Update(existing); }

            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (System.Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}
