using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Application.Common.Validation;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Settings.Commands;

/// <summary>
/// U-MULTI-COMPANY-1 — ساختِ یک شرکتِ کاملاً جدید در همان DBِ مشترک (چند شرکت در یک DB،
/// به‌درخواستِ کاربر). برخلافِ ماژول‌های دیگر، این Command از سشنِ فعلی (ICurrentUserService)
/// مستقل است — کاربر ممکن است هنوز به‌عنوانِ ادمینِ شرکتِ *دیگری* لاگین باشد وقتی شرکتِ نو را
/// می‌سازد (از دکمهٔ «شرکتِ جدید» در صفحهٔ ورود). ادمینِ شرکتِ نو همیشه با نامِ کاربریِ «admin»
/// ساخته می‌شود تا از fallbackِ شناخته‌شدهٔ AuthenticateCommandHandler (دسترسیِ کاملِ wildcard
/// برایِ کاربرِ «admین» بدونِ نیازِ Role/RolePermission) بهره ببرد — بدونِ نیازِ seedِ نقش/مجوز
/// برایِ شرکتِ تازه.
/// </summary>
public record CreateCompanyCommand(
    string Name, string? NationalId, string? EconomicCode, string? Phone, string? Address,
    string FiscalTitle, string FiscalStart, string FiscalEnd, string AdminPassword)
    : IRequest<Result<CreateCompanyResult>>;

public record CreateCompanyResult(int CompanyId, string Code, string Name, int AdminUserId);

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Result<CreateCompanyResult>>
{
    private readonly IRepository<Company> _companies;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly ICompanyProvisioningService _provisioning;

    public CreateCompanyCommandHandler(IRepository<Company> companies, IRepository<Branch> branches,
        IRepository<FiscalYear> fiscalYears, IRepository<User> users, IUnitOfWork uow,
        ICompanyProvisioningService provisioning)
    { _companies = companies; _branches = branches; _fiscalYears = fiscalYears; _users = users; _uow = uow; _provisioning = provisioning; }

    public async Task<Result<CreateCompanyResult>> Handle(CreateCompanyCommand req, CancellationToken ct)
    {
        var name = req.Name?.Trim() ?? "";
        if (name.Length < 2)
            return Result<CreateCompanyResult>.Failure("نامِ معتبرِ شرکت را وارد کنید (دستِ‌کم ۲ نویسه).");

        var (ok, err) = PasswordPolicy.Validate(req.AdminPassword);
        if (!ok) return Result<CreateCompanyResult>.Failure(err!);

        // کدِ خودکار: عددِ بعدیِ آزاد، سه‌رقمی (۰۰۱، ۰۰۲، …) — هم‌راستا با کدِ شرکتِ seedِ اولیه.
        var existing = await _companies.GetAllAsync(ct);
        var nextNumber = existing
            .Select(c => int.TryParse(c.Code, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1;
        var code = nextNumber.ToString("000");

        var company = Company.Create(code, name, req.FiscalStart, req.FiscalEnd);
        company.Update(name, null, req.NationalId, req.EconomicCode, null, req.Address, req.Phone, null, null, null);
        await _companies.AddAsync(company, ct);
        await _uow.SaveChangesAsync(ct);   // برایِ گرفتنِ Idِ واقعیِ شرکت

        // نمودارِ حساب/شعبه/انبارِ پیش‌فرض — بدونِ این، شرکتِ نو قابلِ‌استفاده نیست.
        await _provisioning.ProvisionAsync(ct);

        var branch = await _branches.FindSingleAsync(b => b.CompanyId == company.Id && b.Code == "HQ", ct);

        var fy = FiscalYear.Create(company.Id, req.FiscalTitle, req.FiscalStart, req.FiscalEnd);
        await _fiscalYears.AddAsync(fy, ct);

        var (hash, salt) = PasswordHasher.Create(req.AdminPassword);
        var admin = User.Create(company.Id, branch?.Id, "admin", hash, salt, "مدیر سیستم");
        await _users.AddAsync(admin, ct);

        await _uow.SaveChangesAsync(ct);

        return Result<CreateCompanyResult>.Success(new CreateCompanyResult(company.Id, code, name, admin.Id));
    }
}
