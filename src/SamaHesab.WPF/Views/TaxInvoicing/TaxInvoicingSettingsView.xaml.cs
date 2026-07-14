using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.TaxInvoicing;

namespace SamaHesab.WPF.Views.TaxInvoicing;

public partial class TaxInvoicingSettingsView : UserControl
{
    public TaxInvoicingSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // PasswordBox امنیتاً data-binding مستقیم نمی‌پذیرد؛ همگام‌سازیِ دوطرفه دستی با VM.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TaxInvoicingSettingsViewModel oldVm) oldVm.PropertyChanged -= Vm_PropertyChanged;
        if (e.NewValue is TaxInvoicingSettingsViewModel vm)
        {
            vm.PropertyChanged += Vm_PropertyChanged;
            CertPasswordBox.Password = vm.CertificatePassword ?? "";
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaxInvoicingSettingsViewModel.CertificatePassword)
            && sender is TaxInvoicingSettingsViewModel vm
            && CertPasswordBox.Password != vm.CertificatePassword)
            CertPasswordBox.Password = vm.CertificatePassword ?? "";
    }

    private void CertPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TaxInvoicingSettingsViewModel vm) vm.CertificatePassword = CertPasswordBox.Password;
    }

    private void BrowseCertificate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "انتخابِ فایلِ گواهیِ دیجیتال",
            Filter = "گواهیِ PFX (*.pfx)|*.pfx|همه فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true && DataContext is TaxInvoicingSettingsViewModel vm)
            vm.CertificatePath = dlg.FileName;
    }
}
