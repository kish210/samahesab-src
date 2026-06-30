namespace SamaHesab.WPF.Services;

/// <summary>
/// CORE-UX-NAV (F2-GLOBAL): صفحاتی که میان‌برِ سراسریِ <b>F2 = «جدید»</b> را پشتیبانی می‌کنند.
/// پوستهٔ برنامه (MainWindow) با فشردنِ F2، اگر VMِ تبِ فعال این اینترفیس را پیاده کرده باشد،
/// <see cref="RequestNew"/> را صدا می‌زند — تا هر فهرست بدونِ نیاز به دکمهٔ «جدید» با F2 رکوردِ نو بسازد.
/// </summary>
public interface ISupportsNew
{
    /// <summary>ایجادِ رکوردِ جدید (همان رفتارِ دکمهٔ «جدید» همان صفحه).</summary>
    void RequestNew();
}
