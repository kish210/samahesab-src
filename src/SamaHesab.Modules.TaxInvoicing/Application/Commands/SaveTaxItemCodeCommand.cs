using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Commands;

/// <summary>
/// نگاشتِ یک کالا به شناسهٔ کالایِ رسمی/کدِ واحدِ سامانهٔ مودیان — بدونِ این نگاشت، ردیفِ فاکتورِ
/// شاملِ آن کالا در payload بدونِ itemId/unit ارسال می‌شود (نه استثنا؛ سازمان خودش ردش می‌کند).
/// </summary>
public record SaveTaxItemCodeCommand(int ProductId, string ItemId, string MeasurementUnitCode) : IRequest<Result>;

public class SaveTaxItemCodeCommandHandler : IRequestHandler<SaveTaxItemCodeCommand, Result>
{
    private readonly IRepository<TaxItemCode> _codes;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveTaxItemCodeCommandHandler(IRepository<TaxItemCode> codes, IUnitOfWork uow, ICurrentUserService user)
    { _codes = codes; _uow = uow; _user = user; }

    public async Task<Result> Handle(SaveTaxItemCodeCommand req, CancellationToken ct)
    {
        if (req.ProductId <= 0) return Result.Failure("کالا نامعتبر است.");
        if (string.IsNullOrWhiteSpace(req.ItemId)) return Result.Failure("شناسهٔ کالایِ رسمی الزامی است.");
        if (string.IsNullOrWhiteSpace(req.MeasurementUnitCode)) return Result.Failure("کدِ واحدِ اندازه‌گیری الزامی است.");

        var companyId = _user.CompanyId ?? 1;
        var existing = await _codes.FindSingleAsync(c => c.CompanyId == companyId && c.ProductId == req.ProductId, ct);
        if (existing is null)
            await _codes.AddAsync(TaxItemCode.Create(companyId, req.ProductId, req.ItemId, req.MeasurementUnitCode), ct);
        else
            existing.Update(req.ItemId, req.MeasurementUnitCode);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
