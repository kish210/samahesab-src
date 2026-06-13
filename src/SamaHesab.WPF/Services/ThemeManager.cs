namespace SamaHesab.WPF.Services;

/// <summary>
/// تمِ برنامه پس از حذف Telerik. پوسته با تمِ سفارشیِ Sama (Assets/Themes/*) رندر می‌شود
/// و دیگر تمِ قابل‌سوییچِ Telerik وجود ندارد؛ این کلاس برای سازگاریِ فراخوان‌ها باقی مانده.
/// </summary>
public static class ThemeManager
{
    public static readonly string[] Available = { "Sama" };
    public static string Current { get; private set; } = "Sama";

    /// <summary>بدون Telerik کاری انجام نمی‌دهد (پوسته ثابتِ Sama است).</summary>
    public static void Apply(string theme) { Current = "Sama"; }
}
