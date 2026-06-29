using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TourismItinerary.Domain;

namespace SamaHesab.Modules.TourismItinerary.Application.Itinerary.Commands;

/// <summary>
/// تولیدِ برنامهٔ اقامتیِ پیشنهادی برای یک مهمان: محصولات×سانس‌ها×روزها → نامزدها → الگوریتمِ
/// <see cref="ItineraryPlanner"/> (اولویتِ سود، عدمِ تداخل، تنوع) → ذخیرهٔ GuestItinerary + Stops.
/// خروجی شاملِ توکنِ یکتا برای ساختِ لینکِ پنلِ مهمان است.
/// </summary>
public record GenerateItineraryCommand(
    string GuestName, int Days, int? GuestPartyId = null, int? MaxPerDay = null, bool PreferVariety = true)
    : IRequest<Result<GeneratedItineraryDto>>;

public record GeneratedItineraryDto(
    int ItineraryId, string Token, int Days, int StopCount,
    decimal TotalSale, decimal TotalProfit);

public class GenerateItineraryCommandValidator : AbstractValidator<GenerateItineraryCommand>
{
    public GenerateItineraryCommandValidator()
    {
        RuleFor(x => x.GuestName).NotEmpty().WithMessage("نامِ مهمان الزامی است.");
        RuleFor(x => x.Days).InclusiveBetween(1, 60).WithMessage("تعدادِ روزها باید بین ۱ تا ۶۰ باشد.");
    }
}

public class GenerateItineraryCommandHandler : IRequestHandler<GenerateItineraryCommand, Result<GeneratedItineraryDto>>
{
    private readonly IRepository<ItineraryProduct> _products;
    private readonly IRepository<ProductSession> _sessions;
    private readonly IRepository<GuestItinerary> _itineraries;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IPersianCalendarService _calendar;

    public GenerateItineraryCommandHandler(
        IRepository<ItineraryProduct> products, IRepository<ProductSession> sessions,
        IRepository<GuestItinerary> itineraries, IUnitOfWork uow,
        ICurrentUserService user, IPersianCalendarService calendar)
    {
        _products = products; _sessions = sessions; _itineraries = itineraries;
        _uow = uow; _user = user; _calendar = calendar;
    }

    public async Task<Result<GeneratedItineraryDto>> Handle(GenerateItineraryCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;

            var products = await _products.FindAsync(p => p.CompanyId == companyId && p.Active, ct);
            if (products.Count == 0) return Result<GeneratedItineraryDto>.Failure("هیچ محصولِ اقامتیِ فعالی برای برنامه‌ریزی نیست.");

            var productIds = products.Select(p => p.Id).ToHashSet();
            var sessions = await _sessions.FindAsync(
                s => s.CompanyId == companyId && s.Active && productIds.Contains(s.ProductId), ct);
            if (sessions.Count == 0) return Result<GeneratedItineraryDto>.Failure("هیچ سانسِ فعالی تعریف نشده است.");

            var productById = products.ToDictionary(p => p.Id);

            // نامزدها: هر محصول×سانس برای هر روز (همان سانس می‌تواند در روزهای مختلف پیشنهاد شود؛
            // الگوریتم با اولویتِ تنوع، محصولاتِ متفاوت را میانِ روزها پخش می‌کند).
            var candidates = new List<PlanCandidate>();
            for (int day = 1; day <= req.Days; day++)
                foreach (var s in sessions)
                {
                    var p = productById[s.ProductId];
                    candidates.Add(new PlanCandidate(
                        p.Id, p.Name, s.Id, day, s.StartMinute, s.EndMinute, p.SalePrice, p.Cost));
                }

            var plan = ItineraryPlanner.Plan(candidates, new PlanOptions(req.Days, req.PreferVariety, req.MaxPerDay));
            if (plan.Stops.Count == 0) return Result<GeneratedItineraryDto>.Failure("برنامه‌ای تولید نشد (نامزدِ مناسب نبود).");

            var itinerary = GuestItinerary.Create(companyId, req.GuestName, req.Days,
                _calendar.GetCurrentPersianDate(), req.GuestPartyId);
            foreach (var st in plan.Stops)
                itinerary.AddStop(ItineraryStop.Create(
                    st.ProductId, st.SessionId, st.Day, st.SortOrder, st.StartMinute, st.EndMinute, st.SalePrice, st.Cost));

            await _itineraries.AddAsync(itinerary, ct);
            await _uow.SaveChangesAsync(ct);

            return Result<GeneratedItineraryDto>.Success(new GeneratedItineraryDto(
                itinerary.Id, itinerary.Token, itinerary.Days, plan.Stops.Count, plan.TotalSale, plan.TotalProfit));
        }
        catch (System.Exception ex) { return Result<GeneratedItineraryDto>.Failure(ex.GetBaseException().Message); }
    }
}
