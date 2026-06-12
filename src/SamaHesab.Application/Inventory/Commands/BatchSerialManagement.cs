using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

// ── بچ‌های یک کالا ──────────────────────────────────────────────────────────────
public record GetBatchesQuery(int? ProductId = null) : IRequest<List<BatchDto>>;
public record BatchDto(int Id, int ProductId, string BatchNumber, string? ProductionDate,
    string? ExpiryDate, decimal Quantity, decimal? PurchasePrice, string? Notes);

public class GetBatchesQueryHandler : IRequestHandler<GetBatchesQuery, List<BatchDto>>
{
    private readonly IRepository<Batch> _batches;
    public GetBatchesQueryHandler(IRepository<Batch> batches) => _batches = batches;

    public async Task<List<BatchDto>> Handle(GetBatchesQuery req, CancellationToken ct)
    {
        var list = req.ProductId is int pid
            ? await _batches.FindAsync(b => b.ProductId == pid, ct)
            : await _batches.GetAllAsync(ct);
        return list.OrderBy(b => b.ExpiryDate ?? "9999").ThenBy(b => b.BatchNumber)
            .Select(b => new BatchDto(b.Id, b.ProductId, b.BatchNumber, b.ProductionDate,
                b.ExpiryDate, b.Quantity, b.PurchasePrice, b.Notes)).ToList();
    }
}

// ── بچ‌های رو به انقضا (کنترل انقضا) ────────────────────────────────────────────
public record GetExpiringBatchesQuery(string Today, int HorizonDays = 60) : IRequest<List<ExpiringBatchDto>>;
public record ExpiringBatchDto(int Id, int ProductId, string BatchNumber, string ExpiryDate,
    decimal Quantity, bool IsExpired);

public class GetExpiringBatchesQueryHandler : IRequestHandler<GetExpiringBatchesQuery, List<ExpiringBatchDto>>
{
    private readonly IRepository<Batch> _batches;
    public GetExpiringBatchesQueryHandler(IRepository<Batch> batches) => _batches = batches;

    public async Task<List<ExpiringBatchDto>> Handle(GetExpiringBatchesQuery req, CancellationToken ct)
    {
        // افق بر اساس تاریخ شمسی رشته‌ای: کران بالا را با افزودن روز به تاریخ امروز می‌سازیم.
        var horizon = AddDaysShamsi(req.Today, req.HorizonDays);
        var list = await _batches.FindAsync(
            b => b.Quantity > 0 && b.ExpiryDate != null && b.ExpiryDate.CompareTo(horizon) <= 0, ct);
        return list.OrderBy(b => b.ExpiryDate)
            .Select(b => new ExpiringBatchDto(b.Id, b.ProductId, b.BatchNumber, b.ExpiryDate!,
                b.Quantity, b.ExpiryDate!.CompareTo(req.Today) < 0)).ToList();
    }

    // افزودن روز روی «yyyy/MM/dd» شمسی (با محدودسازی روز به ۲۹ برای ایمنی ماه‌های متغیر).
    private static string AddDaysShamsi(string today, int days)
    {
        var parts = today.Split('/');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var y) ||
            !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var d))
            return "9999/99/99";
        d += days;
        while (d > 30) { d -= 30; m++; if (m > 12) { m -= 12; y++; } }
        if (d > 29) d = 29;
        return $"{y:0000}/{m:00}/{d:00}";
    }
}

// ── ذخیرهٔ بچ ───────────────────────────────────────────────────────────────────
public record SaveBatchCommand(int ProductId, string BatchNumber, string? ProductionDate,
    string? ExpiryDate, decimal Quantity, decimal? PurchasePrice, string? Notes) : IRequest<Result<int>>;

public class SaveBatchCommandValidator : AbstractValidator<SaveBatchCommand>
{
    public SaveBatchCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
        RuleFor(x => x.BatchNumber).NotEmpty().WithMessage("شمارهٔ بچ الزامی است.");
    }
}

public class SaveBatchCommandHandler : IRequestHandler<SaveBatchCommand, Result<int>>
{
    private readonly IRepository<Batch> _batches;
    private readonly IUnitOfWork _uow;
    public SaveBatchCommandHandler(IRepository<Batch> batches, IUnitOfWork uow)
    { _batches = batches; _uow = uow; }

    public async Task<Result<int>> Handle(SaveBatchCommand req, CancellationToken ct)
    {
        try
        {
            if (await _batches.AnyAsync(b => b.ProductId == req.ProductId && b.BatchNumber == req.BatchNumber, ct))
                return Result<int>.Failure("شمارهٔ بچ برای این کالا تکراری است.");
            var batch = Batch.Create(req.ProductId, req.BatchNumber, req.ProductionDate,
                req.ExpiryDate, req.Quantity, req.PurchasePrice);
            await _batches.AddAsync(batch, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(batch.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── سریال‌های یک کالا ───────────────────────────────────────────────────────────
public record GetSerialsQuery(int? ProductId = null, SerialStatus? Status = null) : IRequest<List<SerialDto>>;
public record SerialDto(int Id, int ProductId, int? WarehouseId, string SerialNumber,
    string Status, decimal? PurchasePrice, string? PurchaseDate, string? SaleDate);

public class GetSerialsQueryHandler : IRequestHandler<GetSerialsQuery, List<SerialDto>>
{
    private readonly IRepository<Serial> _serials;
    public GetSerialsQueryHandler(IRepository<Serial> serials) => _serials = serials;

    private static string Fa(SerialStatus s) => s switch
    {
        SerialStatus.Sold => "فروخته شده",
        SerialStatus.Defective => "معیوب",
        _ => "موجود"
    };

    public async Task<List<SerialDto>> Handle(GetSerialsQuery req, CancellationToken ct)
    {
        var list = await _serials.FindAsync(s =>
            (req.ProductId == null || s.ProductId == req.ProductId) &&
            (req.Status == null || s.Status == req.Status), ct);
        return list.OrderBy(s => s.SerialNumber)
            .Select(s => new SerialDto(s.Id, s.ProductId, s.WarehouseId, s.SerialNumber,
                Fa(s.Status), s.PurchasePrice, s.PurchaseDate, s.SaleDate)).ToList();
    }
}

// ── ذخیرهٔ سریال ────────────────────────────────────────────────────────────────
public record SaveSerialCommand(int ProductId, string SerialNumber, int? WarehouseId,
    decimal? PurchasePrice, string? PurchaseDate) : IRequest<Result<int>>;

public class SaveSerialCommandValidator : AbstractValidator<SaveSerialCommand>
{
    public SaveSerialCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
        RuleFor(x => x.SerialNumber).NotEmpty().WithMessage("شمارهٔ سریال الزامی است.");
    }
}

public class SaveSerialCommandHandler : IRequestHandler<SaveSerialCommand, Result<int>>
{
    private readonly IRepository<Serial> _serials;
    private readonly IUnitOfWork _uow;
    public SaveSerialCommandHandler(IRepository<Serial> serials, IUnitOfWork uow)
    { _serials = serials; _uow = uow; }

    public async Task<Result<int>> Handle(SaveSerialCommand req, CancellationToken ct)
    {
        try
        {
            if (await _serials.AnyAsync(s => s.ProductId == req.ProductId && s.SerialNumber == req.SerialNumber, ct))
                return Result<int>.Failure("شمارهٔ سریال برای این کالا تکراری است.");
            var serial = Serial.Create(req.ProductId, req.SerialNumber, req.WarehouseId,
                req.PurchasePrice, req.PurchaseDate);
            await _serials.AddAsync(serial, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(serial.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
