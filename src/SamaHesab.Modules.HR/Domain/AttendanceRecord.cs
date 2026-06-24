using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

public class AttendanceRecord : BaseEntity
{
    public int EmployeeId { get; private set; }
    public string WorkDate { get; private set; } = default!;
    public TimeOnly? CheckIn { get; private set; }
    public TimeOnly? CheckOut { get; private set; }
    public decimal? WorkHours { get; private set; }
    public decimal? OvertimeHours { get; private set; }
    public string Status { get; private set; } = "حاضر";
    public string? LeaveType { get; private set; }
    public string? Notes { get; private set; }

    private AttendanceRecord() { }

    public static AttendanceRecord Create(int employeeId, string workDate, string status = "حاضر")
    {
        return new AttendanceRecord { EmployeeId = employeeId, WorkDate = workDate, Status = status };
    }

    public void SetCheckIn(TimeOnly checkIn) { CheckIn = checkIn; RecalculateHours(); }

    public void SetCheckOut(TimeOnly checkOut)
    {
        CheckOut = checkOut;
        RecalculateHours();
    }

    private void RecalculateHours()
    {
        if (CheckIn.HasValue && CheckOut.HasValue && CheckOut > CheckIn)
        {
            var totalHours = (decimal)(CheckOut.Value - CheckIn.Value).TotalHours;
            WorkHours = Math.Min(totalHours, 8);
            OvertimeHours = Math.Max(0, totalHours - 8);
        }
    }

    public void SetLeave(string leaveType, string? notes = null)
    {
        Status = "مرخصی";
        LeaveType = leaveType;
        Notes = notes;
    }

    public void SetAbsent(string? notes = null)
    {
        Status = "غایب";
        Notes = notes;
    }
}
