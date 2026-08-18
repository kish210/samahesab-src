using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Settings;

/// <summary>CR-X8 — تنظیماتِ شرکتیِ کلید-مقدار در DB (همگامیِ چندایستگاهی).</summary>
public static class CompanySettingKeys
{
    public const string EnforceSoD = "EnforceSoD";   // تفکیکِ وظایف: ثبت‌کننده نمی‌تواند سندِ خود را تأیید کند
    public const string CompanyName = "CompanyName";
    public const string CompanyNationalId = "CompanyNationalId";
    public const string CompanyEconomicCode = "CompanyEconomicCode";
    public const string CompanyPhone = "CompanyPhone";
    public const string CompanyAddress = "CompanyAddress";

    /// <summary>صنف/شغلِ شرکت (راه‌اندازیِ اولیه) — برای پیش‌پُرِ کالاهای نمونهٔ متناسب و گزارش‌های BI.</summary>
    public const string BusinessType = "BusinessType";

    /// <summary>لوگویِ سربرگِ چاپ به‌صورتِ data-URI (کوچک‌شده در مرورگر پیش از ارسال).
    /// در همین جدولِ کلید-مقدار می‌ماند چون ستونِ Value از نوعِ nvarchar(max) است و
    /// این‌طور نیازی به جدول/مسیرِ استاتیکِ جدا برایِ یک تصویرِ کوچک نیست.</summary>
    public const string CompanyLogo = "CompanyLogo";

    /// <summary>کلیدِ ماژول‌هایِ غیرفعال‌شده (comma-separated). غیرفعال‌سازی سطحِ نمایش است:
    /// ماژول از منو/capabilities پنهان می‌شود و دادهٔ تاریخی‌اش دست‌نخورده می‌ماند (قاعدهٔ removability).</summary>
    public const string DisabledModules = "DisabledModules";

    /// <summary>"true" پس از اتمام/ردِ ویزاردِ راه‌اندازیِ اولیهٔ وب (U-WEB-WIZARD) — معادلِ
    /// `AppSettingsStore.SetupCompleted`ِ دسکتاپ که فایلِ محلیِ per-machine بود، اینجا سطحِ
    /// شرکت است (چون وب می‌تواند از چند ماشین باز شود).</summary>
    public const string SetupCompleted = "SetupCompleted";
}

/// <summary>خواندن/نوشتنِ مجموعهٔ کلیدِ ماژول‌هایِ غیرفعال از تنظیماتِ شرکتی (comma-separated).</summary>
public static class DisabledModulesHelper
{
    public static HashSet<string> Parse(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string ToCsv(IEnumerable<string> keys) => string.Join(",", keys.Distinct(StringComparer.OrdinalIgnoreCase));
}

/// <summary>خواندنِ یک تنظیمِ شرکتی از DB با پیش‌فرض — قابلِ استفاده توسطِ هندلرهای Application.</summary>
public static class CompanySettingsReader
{
    public static async Task<string?> GetAsync(IRepository<CompanySetting> repo, int companyId, string key,
        string? fallback = null, CancellationToken ct = default)
    {
        var row = await repo.FindSingleAsync(s => s.CompanyId == companyId && s.Key == key, ct);
        return row?.Value ?? fallback;
    }

    public static async Task<bool> GetBoolAsync(IRepository<CompanySetting> repo, int companyId, string key,
        bool fallback = false, CancellationToken ct = default)
    {
        var v = await GetAsync(repo, companyId, key, null, ct);
        return v is null ? fallback : (v == "true" || v == "1" || v == "True");
    }
}

public record GetCompanySettingsQuery() : IRequest<Dictionary<string, string?>>;

public class GetCompanySettingsQueryHandler : IRequestHandler<GetCompanySettingsQuery, Dictionary<string, string?>>
{
    private readonly IRepository<CompanySetting> _settings;
    private readonly ICurrentUserService _user;
    public GetCompanySettingsQueryHandler(IRepository<CompanySetting> settings, ICurrentUserService user)
    { _settings = settings; _user = user; }

    public async Task<Dictionary<string, string?>> Handle(GetCompanySettingsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        return (await _settings.FindAsync(s => s.CompanyId == companyId, ct))
            .ToDictionary(s => s.Key, s => s.Value);
    }
}

public record SaveCompanySettingCommand(string Key, string? Value) : IRequest<Result<int>>;

public class SaveCompanySettingCommandHandler : IRequestHandler<SaveCompanySettingCommand, Result<int>>
{
    private readonly IRepository<CompanySetting> _settings;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveCompanySettingCommandHandler(IRepository<CompanySetting> settings, IUnitOfWork uow, ICurrentUserService user)
    { _settings = settings; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveCompanySettingCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Key)) return Result<int>.Failure("کلیدِ تنظیم الزامی است.");
        var companyId = _user.CompanyId ?? 1;
        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId && s.Key == req.Key, ct);
        var isNew = row is null;
        row ??= CompanySetting.Create(companyId, req.Key, req.Value);
        if (!isNew) row.SetValue(req.Value);
        if (isNew) await _settings.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(row.Id);
    }
}
