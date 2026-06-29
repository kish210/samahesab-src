using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Tourism.Domain;

namespace SamaHesab.Modules.Tourism.Application.Itinerary;

// ───────────────────────── کوئریِ مشاهدهٔ برنامه (پنلِ مهمان) ─────────────────────────

/// <summary>
/// دریافتِ برنامهٔ اقامتیِ مهمان با توکنِ یکتا (لینکِ پنلِ مهمان). بدونِ نیاز به احراز هویت:
/// توکنِ GUID خودش کلیدِ دسترسی است (در درخواستِ ناشناس فیلترِ multi-tenant غیرفعال است، پس
/// جستجو بر توکن کار می‌کند؛ چون توکن غیرقابلِ‌حدس است، مهمان فقط برنامهٔ خودش را می‌بیند).
/// </summary>
public record GetGuestItineraryQuery(string Token) : IRequest<Result<GuestItineraryDto>>;

public record GuestStopDto(
    int StopId, int Day, int SortOrder, int ProductId, string ProductName,
    int SessionId, string SessionLabel, int StartMinute, int EndMinute, decimal SalePrice);

public record GuestItineraryDto(
    string Token, string GuestName, int Days, string Status, string CreatedDate,
    string? Notes, decimal TotalSale, IReadOnlyList<GuestStopDto> Stops);

public class GetGuestItineraryQueryHandler : IRequestHandler<GetGuestItineraryQuery, Result<GuestItineraryDto>>
{
    private readonly IRepository<GuestItinerary> _itineraries;
    private readonly IRepository<ItineraryStop> _stops;
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<ProductSession> _sessions;

    public GetGuestItineraryQueryHandler(IRepository<GuestItinerary> itineraries, IRepository<ItineraryStop> stops,
        IRepository<TourismProduct> products, IRepository<ProductSession> sessions)
    { _itineraries = itineraries; _stops = stops; _products = products; _sessions = sessions; }

    public async Task<Result<GuestItineraryDto>> Handle(GetGuestItineraryQuery req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token)) return Result<GuestItineraryDto>.Failure("توکن نامعتبر است.");

        var it = await _itineraries.FindSingleAsync(g => g.Token == req.Token, ct);
        if (it is null) return Result<GuestItineraryDto>.Failure("برنامه‌ای با این لینک یافت نشد.");

        var stops = await _stops.FindAsync(s => s.ItineraryId == it.Id, ct);
        var productIds = stops.Select(s => s.ProductId).ToHashSet();
        var sessionIds = stops.Select(s => s.SessionId).ToHashSet();
        var pNames = (await _products.FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.Name);
        var sLabels = (await _sessions.FindAsync(s => sessionIds.Contains(s.Id), ct)).ToDictionary(s => s.Id, s => s.Label);

        var dto = new GuestItineraryDto(
            it.Token, it.GuestName, it.Days, it.Status.ToString(), it.CreatedDate, it.Notes, it.TotalSale,
            stops.OrderBy(s => s.DayNumber).ThenBy(s => s.SortOrder)
                .Select(s => new GuestStopDto(
                    s.Id, s.DayNumber, s.SortOrder, s.ProductId, pNames.GetValueOrDefault(s.ProductId, $"#{s.ProductId}"),
                    s.SessionId, sLabels.GetValueOrDefault(s.SessionId, ""), s.StartMinute, s.EndMinute, s.SalePrice))
                .ToList());
        return Result<GuestItineraryDto>.Success(dto);
    }
}

// ───────────────────────── ویرایش/تأییدِ مهمان (پنلِ مهمان) ─────────────────────────

/// <summary>
/// ویرایش (حذفِ برخی اقلام) و/یا تأییدِ نهاییِ برنامه توسطِ مهمان — با توکنِ یکتا.
/// Confirm=true → وضعیت Confirmed؛ وگرنه GuestEdited (هنوز قابلِ تغییر). اقلامِ حذف‌شده پاک می‌شوند.
/// </summary>
public record SubmitGuestItineraryCommand(
    string Token, IReadOnlyList<int> RemovedStopIds, bool Confirm, string? Notes = null)
    : IRequest<Result>;

public class SubmitGuestItineraryCommandValidator : AbstractValidator<SubmitGuestItineraryCommand>
{
    public SubmitGuestItineraryCommandValidator()
        => RuleFor(x => x.Token).NotEmpty().WithMessage("توکن الزامی است.");
}

public class SubmitGuestItineraryCommandHandler : IRequestHandler<SubmitGuestItineraryCommand, Result>
{
    private readonly IRepository<GuestItinerary> _itineraries;
    private readonly IRepository<ItineraryStop> _stops;
    private readonly IUnitOfWork _uow;

    public SubmitGuestItineraryCommandHandler(IRepository<GuestItinerary> itineraries,
        IRepository<ItineraryStop> stops, IUnitOfWork uow)
    { _itineraries = itineraries; _stops = stops; _uow = uow; }

    public async Task<Result> Handle(SubmitGuestItineraryCommand req, CancellationToken ct)
    {
        try
        {
            var it = await _itineraries.FindSingleAsync(g => g.Token == req.Token, ct);
            if (it is null) return Result.Failure("برنامه‌ای با این لینک یافت نشد.");
            if (it.Status == ItineraryStatus.Confirmed) return Result.Failure("این برنامه قبلاً تأیید نهایی شده است.");

            if (req.RemovedStopIds is { Count: > 0 })
            {
                var toRemove = await _stops.FindAsync(s => s.ItineraryId == it.Id && req.RemovedStopIds.Contains(s.Id), ct);
                if (toRemove.Count > 0) _stops.RemoveRange(toRemove);
            }

            if (req.Confirm) it.ConfirmByGuest(req.Notes);
            else it.MarkGuestEdited();
            _itineraries.Update(it);

            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (System.Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}
