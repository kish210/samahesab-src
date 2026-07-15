using MediatR;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Settings.Commands;

/// <summary>
/// U-MULTI-COMPANY-1 — پیش‌تر ویزاردِ راه‌اندازیِ اولیه فقط اسمِ شرکت را در تنظیماتِ محلیِ
/// AppSettingsStore ذخیره می‌کرد، نه در ردیفِ واقعیِ Cfg.Companies (که با «شرکت نمونه»ی
/// seedِ اولیه ساخته شده) — نتیجه: صفحهٔ ورود همیشه نامِ seedِ اولیه را نشان می‌داد، نه
/// نامی که کاربر در ویزارد وارد کرده بود. این Command همان ردیفِ موجود را با اطلاعاتِ
/// واقعیِ کاربر به‌روز می‌کند.
/// </summary>
public record UpdateCompanyCommand(int CompanyId, string Name, string? NationalId,
    string? EconomicCode, string? Phone, string? Address) : IRequest<Result>;

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, Result>
{
    private readonly IRepository<Company> _companies;
    private readonly IUnitOfWork _uow;

    public UpdateCompanyCommandHandler(IRepository<Company> companies, IUnitOfWork uow)
    { _companies = companies; _uow = uow; }

    public async Task<Result> Handle(UpdateCompanyCommand req, CancellationToken ct)
    {
        var name = req.Name?.Trim() ?? "";
        if (name.Length < 2) return Result.Failure("نامِ معتبرِ شرکت را وارد کنید (دستِ‌کم ۲ نویسه).");

        var company = await _companies.GetByIdAsync(req.CompanyId, ct);
        if (company is null) return Result.Failure("شرکت یافت نشد.");

        company.Update(name, null, req.NationalId, req.EconomicCode, null, req.Address, req.Phone, null, null, null);
        _companies.Update(company);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
