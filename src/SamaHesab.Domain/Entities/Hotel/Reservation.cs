using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

public enum ReservationSource { Direct = 0, Agency = 1, OTA = 2, Walkin = 3 }
public enum ReservationStatus { Hold = 0, Confirmed = 1, Guaranteed = 2, CheckedIn = 3, CheckedOut = 4, Cancelled = 5, NoShow = 6 }

/// <summary>PMS-C1-1 — سربرگِ رزرو (مهمان/شرکت/آژانس + بازهٔ اقامت + وضعیت).</summary>
public class Reservation : AuditableEntity, IBranchScoped
{
    public int BranchId { get; private set; }
    public int GuestPartyId { get; private set; }
    public int? CompanyPartyId { get; private set; }
    public int? AgentPartyId { get; private set; }
    public ReservationSource Source { get; private set; }
    public string CheckInDate { get; private set; } = default!;   // شمسی
    public string CheckOutDate { get; private set; } = default!;
    public int Nights { get; private set; }
    public int Adults { get; private set; } = 1;
    public int Children { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Hold;
    public string? Notes { get; private set; }

    private readonly List<ReservationRoom> _rooms = new();
    public IReadOnlyCollection<ReservationRoom> Rooms => _rooms.AsReadOnly();

    private Reservation() { }

    public static Reservation Create(int companyId, int branchId, int guestPartyId, ReservationSource source,
        string checkInDate, string checkOutDate, int nights, int adults = 1, int children = 0,
        int? companyPartyId = null, int? agentPartyId = null, string? notes = null)
    {
        if (guestPartyId <= 0) throw new ArgumentException("مهمان الزامی است.");
        if (string.IsNullOrWhiteSpace(checkInDate) || string.IsNullOrWhiteSpace(checkOutDate))
            throw new ArgumentException("تاریخِ ورود/خروج الزامی است.");
        if (nights <= 0) throw new ArgumentException("تعدادِ شب باید بزرگ‌تر از صفر باشد.");
        return new Reservation
        {
            CompanyId = companyId, BranchId = branchId, GuestPartyId = guestPartyId, Source = source,
            CheckInDate = checkInDate, CheckOutDate = checkOutDate, Nights = nights,
            Adults = adults < 1 ? 1 : adults, Children = children < 0 ? 0 : children,
            CompanyPartyId = companyPartyId, AgentPartyId = agentPartyId, Notes = notes
        };
    }

    public void AddRoom(ReservationRoom r) => _rooms.Add(r);
    public void SetStatus(ReservationStatus s) { Status = s; SetAudit(null); }
}
