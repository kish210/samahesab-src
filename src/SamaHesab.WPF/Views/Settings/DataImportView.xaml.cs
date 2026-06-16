using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.Settings;

namespace SamaHesab.WPF.Views.Settings;

/// <summary>فاز ۱۲ G4 — ورودِ دادهٔ اکسل. انتخابِ فایل در code-behind؛ خواندن/درج در VM.</summary>
public partial class DataImportView : UserControl
{
    public DataImportView() => InitializeComponent();

    private void Browse_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "انتخابِ فایلِ اکسل",
            Filter = "اکسل (*.xlsx)|*.xlsx|همه فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true && DataContext is DataImportViewModel vm)
            vm.LoadFile(dlg.FileName);
    }
}
