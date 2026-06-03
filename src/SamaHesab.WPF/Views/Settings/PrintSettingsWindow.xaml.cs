using System.Printing;
using System.Windows;
using SamaHesab.WPF.Services;

namespace SamaHesab.WPF.Views.Settings;

public partial class PrintSettingsWindow : Window
{
    public PrintSettingsWindow()
    {
        InitializeComponent();
        LoadPrinters();
        LoadSettings();
    }

    private void LoadPrinters()
    {
        try
        {
            CmbPrinter.Items.Add("(چاپگر پیش‌فرض سیستم)");
            using var server = new LocalPrintServer();
            foreach (var q in server.GetPrintQueues())
                CmbPrinter.Items.Add(q.Name);
        }
        catch { }
        if (CmbPrinter.SelectedIndex < 0) CmbPrinter.SelectedIndex = 0;
    }

    private void LoadSettings()
    {
        var s = AppSettingsStore.GetPrintSettings();
        CmbPrinter.SelectedItem = string.IsNullOrWhiteSpace(s.PrinterName) ? CmbPrinter.Items[0] : s.PrinterName;
        if (CmbPrinter.SelectedItem == null) CmbPrinter.SelectedIndex = 0;
        CmbPaper.SelectedIndex = (int)s.Paper;
        TxtCopies.Text = s.Copies.ToString();
        ChkDialog.IsChecked = s.ShowDialog;
        ChkLogo.IsChecked = s.ShowLogo;
        TxtHeader.Text = s.HeaderTitle;
        TxtHeader2.Text = s.HeaderLine2;
        TxtPhone.Text = s.Phone;
        TxtAddress.Text = s.Address;
        TxtFooter.Text = s.FooterText;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var printer = CmbPrinter.SelectedIndex <= 0 ? null : CmbPrinter.SelectedItem?.ToString();
        int.TryParse(TxtCopies.Text, out var copies);
        var s = new PrintSettings
        {
            PrinterName = printer,
            Paper = (PaperKind)System.Math.Max(0, CmbPaper.SelectedIndex),
            Copies = copies < 1 ? 1 : copies,
            ShowDialog = ChkDialog.IsChecked == true,
            ShowLogo = ChkLogo.IsChecked == true,
            HeaderTitle = string.IsNullOrWhiteSpace(TxtHeader.Text) ? "سماع رایانه کیش" : TxtHeader.Text,
            HeaderLine2 = TxtHeader2.Text,
            Phone = TxtPhone.Text,
            Address = TxtAddress.Text,
            FooterText = TxtFooter.Text
        };
        AppSettingsStore.SavePrintSettings(s);
        MessageBox.Show("تنظیمات چاپ ذخیره شد.", "تنظیمات چاپ", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
