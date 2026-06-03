namespace SamaHesab.WPF.Services;

public enum PaperKind { A4, A5, Receipt80mm }

/// <summary>User-configurable print settings (persisted in settings.user.json).</summary>
public class PrintSettings
{
    public string? PrinterName { get; set; }          // null/empty → system default
    public PaperKind Paper { get; set; } = PaperKind.A4;
    public int Copies { get; set; } = 1;
    public bool ShowDialog { get; set; } = true;       // show the Windows print dialog first
    public bool ShowLogo { get; set; } = true;

    public string HeaderTitle { get; set; } = "سماع رایانه کیش";
    public string? HeaderLine2 { get; set; } = "سامانه جامع مدیریت کسب‌وکار";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string FooterText { get; set; } = "از خرید شما سپاسگزاریم — kishwifi.com";
}
