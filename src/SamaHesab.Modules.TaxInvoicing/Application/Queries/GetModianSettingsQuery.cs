using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Queries;

/// <summary>بارگذاریِ تنظیماتِ سامانهٔ مودیانِ شرکتِ جاری — برایِ صفحهٔ تنظیمات (فازِ UI).</summary>
public record GetModianSettingsQuery() : IRequest<ModianSettingsDto>;

public record ModianSettingsDto(
    string? TaxMemoryId, bool UseSandbox, string? CertificatePath, string? CertificatePassword, bool Enabled);

public class GetModianSettingsQueryHandler : IRequestHandler<GetModianSettingsQuery, ModianSettingsDto>
{
    private readonly IRepository<ModianSettings> _settings;
    private readonly ICurrentUserService _user;

    public GetModianSettingsQueryHandler(IRepository<ModianSettings> settings, ICurrentUserService user)
    { _settings = settings; _user = user; }

    public async Task<ModianSettingsDto> Handle(GetModianSettingsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var s = await _settings.FindSingleAsync(x => x.CompanyId == companyId, ct);
        return s is null
            ? new ModianSettingsDto(null, true, null, null, false)
            : new ModianSettingsDto(s.TaxMemoryId, s.UseSandbox, s.CertificatePath, s.CertificatePassword, s.Enabled);
    }
}
