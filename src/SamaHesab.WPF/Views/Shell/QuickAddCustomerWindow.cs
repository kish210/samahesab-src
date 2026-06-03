using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>Small popup to quickly add a customer from the sales-invoice screen.</summary>
public class QuickAddCustomerWindow : Window
{
    private readonly ComboBox _type;
    private readonly TextBox _name;
    private readonly TextBox _mobile;
    private readonly TextBlock _status;

    public int? NewCustomerId { get; private set; }

    public QuickAddCustomerWindow()
    {
        Title = "افزودن سریع مشتری";
        Width = 420; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = FlowDirection.RightToLeft;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA));
        FontFamily = new FontFamily("Vazirmatn, Tahoma");

        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = "افزودن مشتری جدید", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });

        root.Children.Add(Label("نوع مشتری"));
        _type = new ComboBox { Margin = new Thickness(0, 0, 0, 10), Height = 32 };
        _type.Items.Add("حقیقی"); _type.Items.Add("حقوقی"); _type.SelectedIndex = 0;
        root.Children.Add(_type);

        root.Children.Add(Label("نام / نام شرکت"));
        _name = new TextBox { Margin = new Thickness(0, 0, 0, 10), Height = 32, Padding = new Thickness(6, 4, 6, 4) };
        root.Children.Add(_name);

        root.Children.Add(Label("موبایل"));
        _mobile = new TextBox { Margin = new Thickness(0, 0, 0, 10), Height = 32, Padding = new Thickness(6, 4, 6, 4) };
        root.Children.Add(_mobile);

        _status = new TextBlock { Foreground = Brushes.IndianRed, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        root.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var save = MakeButton("ذخیره", Color.FromRgb(0x16, 0xA3, 0x4A));
        save.Click += (_, _) => Save();
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

    private void Save()
    {
        var name = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) { _status.Text = "نام مشتری الزامی است."; return; }
        try
        {
            var type = _type.SelectedItem?.ToString() ?? "حقیقی";
            var currentUser = App.GetService<ICurrentUserService>();
            var repo = App.GetService<IRepository<Customer>>();
            var uow = App.GetService<IUnitOfWork>();
            var companyId = currentUser.CompanyId ?? 1;
            var code = "C" + System.DateTime.Now.ToString("yyMMddHHmmss");

            Customer entity = type == "حقوقی"
                ? Customer.Create(companyId, code, type, null, null, name)
                : Customer.Create(companyId, code, type, name, "", null);
            entity.UpdateContactInfo(null, _mobile.Text.Trim(), null, null, null, null, null);

            repo.AddAsync(entity).GetAwaiter().GetResult();
            uow.SaveChangesAsync().GetAwaiter().GetResult();

            NewCustomerId = entity.Id;
            DialogResult = true;
            Close();
        }
        catch (System.Exception ex)
        {
            _status.Text = "خطا: " + ex.GetBaseException().Message;
        }
    }
}
