using MediatR;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

// =============================================================================
// نقاط اتصالِ سمتِ انبار برای جریان خرید/فروش (INV-1 گام۳).
// این فرمان‌ها را مسیرِ ثبت خرید/فروش (لِین C2) صدا می‌زند تا بچ/سریال هماهنگ شود.
// همگی idempotent-ish و تراکنش را از uowِ فراخواننده به ارث می‌برند (SaveChanges را صدا نمی‌زنند).
// =============================================================================

/// <summary>
/// رسیدِ بچ هنگام خرید: اگر بچ با این شماره برای کالا باشد موجودی‌اش زیاد می‌شود،
/// وگرنه بچ جدید ساخته می‌شود. شناسهٔ بچ برمی‌گردد. (SaveChanges توسط فراخواننده.)
/// </summary>
public record ReceiveBatchCommand(int ProductId, string BatchNumber, decimal Quantity,
    string? ProductionDate = null, string? ExpiryDate = null, decimal? PurchasePrice = null)
    : IRequest<Result<int>>;

public class ReceiveBatchCommandHandler : IRequestHandler<ReceiveBatchCommand, Result<int>>
{
    private readonly IRepository<Batch> _batches;
    public ReceiveBatchCommandHandler(IRepository<Batch> batches) => _batches = batches;

    public async Task<Result<int>> Handle(ReceiveBatchCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.BatchNumber)) return Result<int>.Failure("شمارهٔ بچ الزامی است.");
        var existing = await _batches.FindSingleAsync(
            b => b.ProductId == req.ProductId && b.BatchNumber == req.BatchNumber, ct);
        if (existing is not null)
        {
            existing.Increase(req.Quantity);
            _batches.Update(existing);
            return Result<int>.Success(existing.Id);
        }
        var batch = Batch.Create(req.ProductId, req.BatchNumber, req.ProductionDate,
            req.ExpiryDate, req.Quantity, req.PurchasePrice);
        await _batches.AddAsync(batch, ct);
        return Result<int>.Success(batch.Id);
    }
}

/// <summary>کاهش موجودیِ بچ هنگام فروش/حواله. (SaveChanges توسط فراخواننده.)</summary>
public record IssueBatchCommand(int BatchId, decimal Quantity) : IRequest<Result>;

public class IssueBatchCommandHandler : IRequestHandler<IssueBatchCommand, Result>
{
    private readonly IRepository<Batch> _batches;
    public IssueBatchCommandHandler(IRepository<Batch> batches) => _batches = batches;

    public async Task<Result> Handle(IssueBatchCommand req, CancellationToken ct)
    {
        var batch = await _batches.GetByIdAsync(req.BatchId, ct);
        if (batch is null) return Result.Failure("بچ یافت نشد.");
        try { batch.Decrease(req.Quantity); }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
        _batches.Update(batch);
        return Result.Success();
    }
}

/// <summary>علامت‌گذاری سریال به‌عنوان فروخته‌شده هنگام فروش. (SaveChanges توسط فراخواننده.)</summary>
public record SellSerialCommand(int SerialId, string SaleDate) : IRequest<Result>;

public class SellSerialCommandHandler : IRequestHandler<SellSerialCommand, Result>
{
    private readonly IRepository<Serial> _serials;
    public SellSerialCommandHandler(IRepository<Serial> serials) => _serials = serials;

    public async Task<Result> Handle(SellSerialCommand req, CancellationToken ct)
    {
        var serial = await _serials.GetByIdAsync(req.SerialId, ct);
        if (serial is null) return Result.Failure("سریال یافت نشد.");
        serial.Sell(req.SaleDate);
        _serials.Update(serial);
        return Result.Success();
    }
}
