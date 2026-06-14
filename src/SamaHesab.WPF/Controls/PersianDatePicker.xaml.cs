using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SamaHesab.WPF.Controls;

/// <summary>
/// ورودیِ تاریخِ شمسی با پاپ‌آپِ تقویمِ ماهانه (کامپوننتِ مشترک — FND-5/U13).
/// مقدارِ متنیِ «yyyy/MM/dd» را در `PersianDate` نگه می‌دارد و معادلِ میلادی را در `SelectedDate`.
/// </summary>
public partial class PersianDatePicker : System.Windows.Controls.UserControl
{
    private static readonly PersianCalendar _pc = new();
    private static readonly string[] _dayNames = { "ش", "ی", "د", "س", "چ", "پ", "ج" };
    private static readonly string[] _monthNames =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
          "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
    private int _viewYear, _viewMonth;   // ماهِ در حالِ نمایشِ پاپ‌آپ (شمسی)

    public static readonly DependencyProperty PersianDateProperty =
        DependencyProperty.Register(nameof(PersianDate), typeof(string), typeof(PersianDatePicker),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPersianDateChanged));

    public string PersianDate
    {
        get => (string)GetValue(PersianDateProperty);
        set => SetValue(PersianDateProperty, value);
    }

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(PersianDatePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public PersianDatePicker()
    {
        InitializeComponent();
        if (string.IsNullOrWhiteSpace(PersianDate))
            PersianDate = ToPersian(DateTime.Today);
    }

    private static void OnPersianDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PersianDatePicker picker && e.NewValue is string dateStr
            && TryParseToGregorian(dateStr, out var gregorian))
            picker.SelectedDate = gregorian;
    }

    private static string ToPersian(DateTime date)
        => $"{_pc.GetYear(date):D4}/{_pc.GetMonth(date):D2}/{_pc.GetDayOfMonth(date):D2}";

    private static bool TryParseToGregorian(string? persianDate, out DateTime result)
    {
        result = DateTime.MinValue;
        var parts = persianDate?.Split('/');
        if (parts is not { Length: 3 }) return false;
        if (int.TryParse(parts[0], out int y) && int.TryParse(parts[1], out int m) && int.TryParse(parts[2], out int day))
        {
            try { result = _pc.ToDateTime(y, m, day, 0, 0, 0, 0); return true; }
            catch { return false; }
        }
        return false;
    }

    // ── پاپ‌آپِ تقویم ──
    private void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        var baseDate = TryParseToGregorian(PersianDate, out var g) ? g : DateTime.Today;
        _viewYear = _pc.GetYear(baseDate);
        _viewMonth = _pc.GetMonth(baseDate);
        BuildCalendar();
        CalPopup.IsOpen = true;
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e) => ShiftMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ShiftMonth(+1);

    private void ShiftMonth(int delta)
    {
        _viewMonth += delta;
        if (_viewMonth < 1) { _viewMonth = 12; _viewYear--; }
        else if (_viewMonth > 12) { _viewMonth = 1; _viewYear++; }
        BuildCalendar();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        PersianDate = ToPersian(DateTime.Today);
        CalPopup.IsOpen = false;
    }

    private void BuildCalendar()
    {
        HeaderText.Text = $"{_monthNames[_viewMonth - 1]} {_viewYear}";

        if (DayNames.Children.Count == 0)
            foreach (var n in _dayNames)
                DayNames.Children.Add(new TextBlock
                {
                    Text = n, TextAlignment = TextAlignment.Center, FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                    FontFamily = (FontFamily)TryFindResource("VazirFont")
                });

        DaysGrid.Children.Clear();
        var firstDay = _pc.ToDateTime(_viewYear, _viewMonth, 1, 0, 0, 0, 0);
        int offset = SaturdayIndex(_pc.GetDayOfWeek(firstDay));   // شنبه=0 .. جمعه=۶
        int daysInMonth = _pc.GetDaysInMonth(_viewYear, _viewMonth);
        var sel = PersianDate;

        for (int i = 0; i < offset; i++) DaysGrid.Children.Add(new TextBlock());   // خانه‌های خالی

        for (int day = 1; day <= daysInMonth; day++)
        {
            var dStr = $"{_viewYear:D4}/{_viewMonth:D2}/{day:D2}";
            var btn = new Button
            {
                Content = day.ToString(), Height = 28, Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0), FontSize = 12, Margin = new Thickness(1),
                FontFamily = (FontFamily)TryFindResource("VazirFont"), Tag = dStr
            };
            if (dStr == sel)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(0x4B, 0x5F, 0x97));
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
            }
            btn.Click += DayButton_Click;
            DaysGrid.Children.Add(btn);
        }
    }

    private void DayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string d)
        {
            PersianDate = d;
            CalPopup.IsOpen = false;
        }
    }

    /// <summary>نگاشتِ روزِ هفتهٔ میلادی به اندیسِ هفتهٔ شمسی (شنبه=۰).</summary>
    private static int SaturdayIndex(DayOfWeek dow) => ((int)dow + 1) % 7;
}
