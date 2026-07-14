using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Commands;

public record SaveModianSettingsCommand(
    string? TaxMemoryId, bool UseSandbox, string? CertificatePath, string? CertificatePassword, bool Enabled)
    : IRequest<Result>;

public class SaveModianSettingsCommandValidator : AbstractValidator<SaveModianSettingsCommand>
{
    public SaveModianSettingsCommandValidator()
    {
        RuleFor(x => x.TaxMemoryId).NotEmpty().When(x => x.Enabled)
            .WithMessage("برایِ فعال‌سازی، شناسهٔ حافظهٔ مالیاتی الزامی است.");
        RuleFor(x => x.CertificatePath).NotEmpty().When(x => x.Enabled)
            .WithMessage("برایِ فعال‌سازی، مسیرِ فایلِ گواهیِ دیجیتال الزامی است.");
    }
}

public class SaveModianSettingsCommandHandler : IRequestHandler<SaveModianSettingsCommand, Result>
{
    private readonly IRepository<ModianSettings> _settings;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveModianSettingsCommandHandler(
        IRepository<ModianSettings> settings, IUnitOfWork uow, ICurrentUserService user)
    { _settings = settings; _uow = uow; _user = user; }

    public async Task<Result> Handle(SaveModianSettingsCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var existing = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        if (existing is null)
        {
            existing = ModianSettings.Create(companyId);
            existing.Update(req.TaxMemoryId, req.UseSandbox, req.CertificatePath, req.CertificatePassword, req.Enabled);
            await _settings.AddAsync(existing, ct);
        }
        else
        {
            existing.Update(req.TaxMemoryId, req.UseSandbox, req.CertificatePath, req.CertificatePassword, req.Enabled);
            _settings.Update(existing);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
