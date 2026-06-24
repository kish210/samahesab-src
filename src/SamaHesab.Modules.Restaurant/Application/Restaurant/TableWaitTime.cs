namespace SamaHesab.Modules.Restaurant.Application;

/// <summary>وضعیتِ انتظارِ میز — مبنای رنگ‌بندی در نقشهٔ میز.</summary>
public enum TableWaitState { Free = 0, Normal = 1, Warning = 2, Critical = 3 }

/// <summary>ورودیِ یک میز برای محاسبهٔ انتظار (شناسه/برچسب + زمانِ بازشدنِ سفارش).</summary>
public record TableWaitInput(int TableId, string TableLabel, System.DateTime? OpenedAt, bool HasOpenOrder);

/// <summary>نمای انتظارِ یک میز برای نقشهٔ میز.</summary>
public record TableWaitRow(int TableId, string TableLabel, bool Occupied, int ElapsedMinutes, TableWaitState State);

/// <summary>
/// محاسبهٔ زمانِ انتظارِ میز برای رنگ‌بندیِ نقشهٔ میز — منطقِ خالص و تست‌پذیر (رودمپ-رستوران).
/// انتظار = فاصلهٔ اکنون تا بازشدنِ سفارش. عبور از آستانه‌ها وضعیتِ هشدار/بحرانی می‌سازد.
/// میزِ بدونِ سفارشِ باز «آزاد» است.
/// </summary>
public static class TableWaitTime
{
    public static IReadOnlyList<TableWaitRow> Build(
        IEnumerable<TableWaitInput> tables, System.DateTime now,
        int warningMinutes = 30, int criticalMinutes = 60)
    {
        var rows = new List<TableWaitRow>();
        foreach (var t in tables)
        {
            if (!t.HasOpenOrder || t.OpenedAt is null)
            {
                rows.Add(new TableWaitRow(t.TableId, t.TableLabel, false, 0, TableWaitState.Free));
                continue;
            }

            var elapsed = (int)System.Math.Max(0, (now - t.OpenedAt.Value).TotalMinutes);
            var state = elapsed >= criticalMinutes ? TableWaitState.Critical
                      : elapsed >= warningMinutes ? TableWaitState.Warning
                      : TableWaitState.Normal;
            rows.Add(new TableWaitRow(t.TableId, t.TableLabel, true, elapsed, state));
        }
        return rows;
    }
}
