using System.Globalization;

namespace SamaHesab.Application.HRM;

/// <summary>وضعیتِ یک خانهٔ تقویم — مبنای رنگ‌بندیِ مغایرت در UI.</summary>
public enum AttendanceCellState { None = 0, Present = 1, Absent = 2, Leave = 3, Late = 4, Holiday = 5 }

/// <summary>یک رکوردِ ورودیِ حضور (کارمند + تاریخِ شمسی + وضعیت + ساعتِ ورود).</summary>
public record AttendanceInput(int EmployeeId, string WorkDate, string Status, TimeOnly? CheckIn);

/// <summary>کارمندِ ردیفِ تقویم.</summary>
public record AttendanceEmployeeRef(int Id, string Name);

public record AttendanceCalendarCell(int Day, AttendanceCellState State);

public record AttendanceCalendarRow(int EmployeeId, string EmployeeName,
    IReadOnlyList<AttendanceCalendarCell> Cells,
    int PresentDays, int AbsentDays, int LeaveDays, int LateDays);

public record AttendanceCalendarResult(int Year, int Month, int DaysInMonth,
    IReadOnlyList<AttendanceCalendarRow> Rows, IReadOnlyList<int> DailyPresentCounts);

/// <summary>
/// نمای تقویمیِ ماهانهٔ حضوروغیاب — منطقِ خالص و تست‌پذیر. رودمپ-حضور: «شبکهٔ روز×پرسنل
/// به‌جای فقط فهرست + علامتِ مغایرت (غیبت/تأخیر)». رکوردهای ماه را به شبکهٔ روز×کارمند می‌نشاند.
/// وضعیت‌های رشته‌ایِ دامنه: «حاضر» / «غایب» / «مرخصی».
/// </summary>
public static class MonthlyAttendanceCalendar
{
    public const string Present = "حاضر";
    public const string Absent = "غایب";
    public const string Leave = "مرخصی";

    public static int DaysInJalaliMonth(int year, int month)
    {
        if (month < 1 || month > 12) return 0;
        try { return new PersianCalendar().GetDaysInMonth(year, month); }
        catch { return 0; }
    }

    /// <summary>روزِ تاریخِ شمسیِ yyyy/MM/dd اگر در سال/ماهِ خواسته باشد؛ وگرنه null.</summary>
    private static int? DayInMonth(string? workDate, int year, int month)
    {
        if (string.IsNullOrWhiteSpace(workDate)) return null;
        var p = workDate.Split('/');
        if (p.Length != 3) return null;
        if (!int.TryParse(p[0], out var y) || !int.TryParse(p[1], out var m) || !int.TryParse(p[2], out var d))
            return null;
        return (y == year && m == month) ? d : (int?)null;
    }

    public static AttendanceCalendarResult Build(
        int year, int month,
        IEnumerable<AttendanceEmployeeRef> employees,
        IEnumerable<AttendanceInput> records,
        TimeOnly? lateThreshold = null,
        ISet<int>? holidays = null)
    {
        var days = DaysInJalaliMonth(year, month);
        var emps = employees.ToList();
        var holi = holidays ?? new HashSet<int>();

        // ایندکسِ رکوردها بر (کارمند، روز) — آخرین رکوردِ روز ملاک است.
        var byKey = new Dictionary<(int emp, int day), AttendanceInput>();
        foreach (var rec in records)
        {
            var day = DayInMonth(rec.WorkDate, year, month);
            if (day is null || day < 1 || day > days) continue;
            byKey[(rec.EmployeeId, day.Value)] = rec;
        }

        var dailyPresent = new int[days + 1];   // ایندکسِ ۱..days
        var rows = new List<AttendanceCalendarRow>();

        foreach (var e in emps)
        {
            var cells = new List<AttendanceCalendarCell>(days);
            int present = 0, absent = 0, leave = 0, late = 0;

            for (int d = 1; d <= days; d++)
            {
                AttendanceCellState state;
                if (byKey.TryGetValue((e.Id, d), out var rec))
                {
                    state = rec.Status switch
                    {
                        Absent => AttendanceCellState.Absent,
                        Leave => AttendanceCellState.Leave,
                        Present => (lateThreshold.HasValue && rec.CheckIn.HasValue && rec.CheckIn.Value > lateThreshold.Value)
                            ? AttendanceCellState.Late : AttendanceCellState.Present,
                        _ => AttendanceCellState.Present
                    };
                }
                else
                {
                    state = holi.Contains(d) ? AttendanceCellState.Holiday : AttendanceCellState.None;
                }

                switch (state)
                {
                    case AttendanceCellState.Present: present++; dailyPresent[d]++; break;
                    case AttendanceCellState.Late: late++; present++; dailyPresent[d]++; break;
                    case AttendanceCellState.Absent: absent++; break;
                    case AttendanceCellState.Leave: leave++; break;
                }

                cells.Add(new AttendanceCalendarCell(d, state));
            }

            rows.Add(new AttendanceCalendarRow(e.Id, e.Name, cells, present, absent, leave, late));
        }

        return new AttendanceCalendarResult(year, month, days, rows, dailyPresent.Skip(1).ToList());
    }
}
