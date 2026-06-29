using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>
/// یک قلمِ برنامهٔ اقامتی: محصول+سانس در یک روزِ مشخص. فرزندِ <see cref="GuestItinerary"/> است
/// (BaseEntity، بدونِ CompanyId — مثلِ ReservationRoom در ماژولِ هتل). قیمت/هزینه snapshot می‌شوند
/// تا تغییرِ بعدیِ محصول، برنامهٔ تأییدشدهٔ مهمان را عوض نکند.
/// </summary>
public class ItineraryStop : BaseEntity
{
    public int ItineraryId { get; private set; }
    public int ProductId { get; private set; }
    public int SessionId { get; private set; }
    public int DayNumber { get; private set; }      // روزِ ۱..N
    public int SortOrder { get; private set; }
    public int StartMinute { get; private set; }    // snapshotِ زمانِ سانس (برای نمایش/بررسیِ تداخل)
    public int EndMinute { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal Cost { get; private set; }

    private ItineraryStop() { }

    public static ItineraryStop Create(int productId, int sessionId, int dayNumber, int sortOrder,
        int startMinute, int endMinute, decimal salePrice, decimal cost)
    {
        if (productId <= 0) throw new ArgumentException("محصول الزامی است.");
        if (sessionId <= 0) throw new ArgumentException("سانس الزامی است.");
        if (dayNumber <= 0) throw new ArgumentException("شمارهٔ روز باید مثبت باشد.");
        return new ItineraryStop
        {
            ProductId = productId, SessionId = sessionId, DayNumber = dayNumber, SortOrder = sortOrder,
            StartMinute = startMinute, EndMinute = endMinute, SalePrice = salePrice, Cost = cost
        };
    }

    internal void SetItineraryId(int itineraryId) => ItineraryId = itineraryId;
}
