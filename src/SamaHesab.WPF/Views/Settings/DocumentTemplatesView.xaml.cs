using System.Windows;
using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.Settings;

namespace SamaHesab.WPF.Views.Settings;

public partial class DocumentTemplatesView : UserControl
{
    public DocumentTemplatesView() => InitializeComponent();

    // DT-5 — ورودِ قالب از فایلِ .shtpl (انتخابِ فایل در code-behind؛ منطق در VM).
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ورودِ قالبِ سند",
            Filter = "قالبِ سما حساب (*.shtpl)|*.shtpl|JSON (*.json)|*.json|همه فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true && DataContext is DocumentTemplatesViewModel vm)
            await vm.ImportFromJsonAsync(System.IO.File.ReadAllText(dlg.FileName));
    }
}
