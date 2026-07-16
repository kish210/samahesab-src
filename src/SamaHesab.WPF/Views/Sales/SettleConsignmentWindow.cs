using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Commands;

namespace SamaHesab.WPF.Views.Sales;

/// <summary>
/// U-CONSIGN-SETTLE — دیالوگِ تسویهٔ یک کنسینمنتِ بازِ مشخص: تاریخِ تسویه + روشِ پرداخت +
/// مبلغِ دریافتی (اختیاری، مابقی نسیه/دریافتنی می‌ماند). ساختِ کدی هم‌الگو با QuickAddCustomerWindow.
/// </summary>
public class SettleConsignmentWindow : Window
{
    private readonly int _invoiceId;
    private readonly TextBox _date;
    private readonly ComboBox _paymentMethod;
    private readonly TextBox _paidAmount;
    private readonly TextBlock _status;

    public SettleConsignmentWindow(int invoiceId, string invoiceNumber, decimal remainAmount)
    {
        _invoiceId = invoiceId;
        Title = "تسویهٔ کنسینمنت";
        Width = 380; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = FlowDirection.RightToLeft;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA));
        FontFamily = new FontFamily("Vazirmatn, Tahoma");

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = $"تسویهٔ کنسینمنتِ {invoiceNumber}",
            FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14)
        });

        root.Children.Add(Label("تاریخِ تسویه"));
        var calendar = App.GetService<IPersianCalendarService>();
        _date = new TextBox
        {
            Text = calendar.GetCurrentPersianDate(), Margin = new Thickness(0, 0, 0, 10),
            Height = 32, Padding = new Thickness(6, 4, 6, 4), FlowDirection = FlowDirection.LeftToRight
        };
        root.Children.Add(_date);

        root.Children.Add(Label("روشِ پرداخت"));
        _paymentMethod = new ComboBox { Margin = new Thickness(0, 0, 0, 10), Height = 32 };
        foreach (var m in new[] { "نسیه", "نقدی", "بانک", "چک" }) _paymentMethod.Items.Add(m);
        _paymentMethod.SelectedIndex = 0;
        root.Children.Add(_paymentMethod);

        root.Children.Add(Label($"مبلغِ دریافتی (مانده: {remainAmount:#,##0})"));
        _paidAmount = new TextBox
        {
            Text = "0", Margin = new Thickness(0, 0, 0, 10),
            Height = 32, Padding = new Thickness(6, 4, 6, 4), FlowDirection = FlowDirection.LeftToRight
        };
        root.Children.Add(_paidAmount);

        _status = new TextBlock { Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        root.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var save = MakeButton("ثبتِ تسویه", Color.FromRgb(0x16, 0xA3, 0x4A));
        save.Click += (_, _) => Settle();
        var cancel = MakeButton("انصراف", Color.FromRgb(0x6B, 0x72, 0x80));
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(save); buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = root;
    }

    private static TextBlock Label(string t) => new()
    { Text = t, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), Margin = new Thickness(0, 0, 0, 3) };

    private static Button MakeButton(string text, Color c) => new()
    {
        Content = text, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(18, 7, 18, 7),
        Foreground = Brushes.White, Background = new SolidColorBrush(c), BorderThickness = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private void Settle()
    {
        if (string.IsNullOrWhiteSpace(_date.Text)) { _status.Text = "تاریخِ تسویه الزامی است."; return; }
        if (!decimal.TryParse(_paidAmount.Text, out var paid) || paid < 0)
        { _status.Text = "مبلغِ دریافتی نامعتبر است."; return; }

        try
        {
            var mediator = App.GetService<IMediator>();
            var result = mediator.Send(new SettleConsignmentCommand(
                _invoiceId, _date.Text.Trim(), paid, _paymentMethod.SelectedItem?.ToString() ?? "نسیه"))
                .GetAwaiter().GetResult();

            if (!result.Succeeded) { _status.Text = result.ErrorMessage; return; }

            DialogResult = true;
            Close();
        }
        catch (System.Exception ex)
        {
            _status.Text = "خطا: " + ex.GetBaseException().Message;
        }
    }
}
