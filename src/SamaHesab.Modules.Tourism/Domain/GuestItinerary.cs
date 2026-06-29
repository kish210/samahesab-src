using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>وضعیتِ برنامهٔ اقامتیِ مهمان.</summary>
public enum ItineraryStatus
{
    Draft = 0,        // پیش‌نویس (تولیدشده، هنوز برای مهمان ارسال نشده)
    Sent = 1,         // لینک برای مهمان ارسال شد
    GuestEdited = 2,  // مهمان ویرایش کرد
    Confirmed = 3     // مهمان تأیید نهایی کرد
}

/// <summary>
/// برنامهٔ اقامتیِ پیشنهادی برای یک مهمان (سرسند). با یک توکنِ یکتا لینکِ پنلِ وب برای مهمان ساخته می‌شود؛
/// مهمان برنامه را می‌بیند/ویرایش/تأیید می‌کند. اقلامِ برنامه در <see cref="ItineraryStop"/> هستند.
/// </summary>
public class GuestItinerary : AuditableEntity
{
    public string GuestName { get; private set; } = default!;
    public int? GuestPartyId { get; private set; }
    public string Token { get; private set; } = default!;   // یکتا — کلیدِ لینکِ پنلِ مهمان
    public int Days { get; private set; }
    public string CreatedDate { get; private set; } = default!;  // شمسی
    public ItineraryStatus Status { get; private set; } = ItineraryStatus.Draft;
    public string? Notes { get; private set; }

    private readonly List<ItineraryStop> _stops = new();
    public IReadOnlyCollection<ItineraryStop> Stops => _stops.AsReadOnly();

    public decimal TotalSale => _stops.Sum(s => s.SalePrice);
    public decimal TotalProfit => _stops.Sum(s => s.SalePrice - s.Cost);

    private GuestItinerary() { }

    public static GuestItinerary Create(int companyId, string guestName, int days, string createdDate,
        int? guestPartyId = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(guestName)) throw new ArgumentException("نامِ مهمان الزامی است.");
        if (days <= 0) throw new ArgumentException("تعدادِ روزها باید مثبت باشد.");
        return new GuestItinerary
        {
            CompanyId = companyId, GuestName = guestName.Trim(), Days = days,
            CreatedDate = createdDate, GuestPartyId = guestPartyId, Notes = notes,
            Token = Guid.NewGuid().ToString("N"), Status = ItineraryStatus.Draft
        };
    }

    public void AddStop(ItineraryStop stop) => _stops.Add(stop);

    /// <summary>جایگزینیِ کاملِ اقلام (هنگامِ تولیدِ دوبارهٔ پیشنهاد یا ویرایشِ مهمان).</summary>
    public void ReplaceStops(IEnumerable<ItineraryStop> stops)
    {
        _stops.Clear();
        _stops.AddRange(stops);
    }

    public void MarkSent() => Status = ItineraryStatus.Sent;

    /// <summary>ثبتِ ویرایشِ مهمان (وضعیت به GuestEdited می‌رود مگر آنکه قبلاً تأیید شده باشد).</summary>
    public void MarkGuestEdited()
    {
        if (Status != ItineraryStatus.Confirmed) Status = ItineraryStatus.GuestEdited;
    }

    public void ConfirmByGuest(string? notes = null)
    {
        Status = ItineraryStatus.Confirmed;
        if (notes is not null) Notes = notes;
    }
}
