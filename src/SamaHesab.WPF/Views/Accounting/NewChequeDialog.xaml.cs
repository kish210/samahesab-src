using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using SamaHesab.Domain.Enums;

namespace SamaHesab.WPF.Views.Accounting;

/// <summary>BUG-4 — دیالوگِ «چک جدید» (لِینِ UIِ C2). خروجی به RegisterChequeCommand داده می‌شود.</summary>
public partial class NewChequeDialog : Window
{
    public sealed record PartyItem(int Id, string Name);

    public ChequeType ChequeType { get; private set; }
    public string PartyTypeLabel { get; private set; } = "مشتری";
    public int PartyId { get; private set; }
    public string ChequeNumber { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string DueDate { get; private set; } = string.Empty;
    public string Date { get; private set; } = string.Empty;
    public string? IssuedBy { get; private set; }
    public string? Description { get; private set; }

    public NewChequeDialog(IEnumerable<PartyItem> parties, string today)
    {
        InitializeComponent();
        CmbParty.ItemsSource = parties.ToList();
        TxtDate.Text = today;
        TxtDue.Text = today;
    }

    private static decimal Parse(string? s)
        => decimal.TryParse((s ?? "").Replace(",", "").Replace("٬", "").Trim(),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string Err(string m) { TxtError.Text = m; TxtError.Visibility = Visibility.Visible; return m; }

        if (CmbParty.SelectedValue is not int pid || pid <= 0) { Err("طرف‌حساب را انتخاب کنید."); return; }
        if (string.IsNullOrWhiteSpace(TxtNumber.Text)) { Err("شمارهٔ چک الزامی است."); TxtNumber.Focus(); return; }
        if (string.IsNullOrWhiteSpace(TxtBank.Text)) { Err("نامِ بانک الزامی است."); TxtBank.Focus(); return; }
        var amount = Parse(TxtAmount.Text);
        if (amount <= 0) { Err("مبلغِ معتبر وارد کنید."); TxtAmount.Focus(); return; }
        if (string.IsNullOrWhiteSpace(TxtDue.Text)) { Err("تاریخِ سررسید الزامی است."); return; }

        ChequeType = RbPaid.IsChecked == true ? ChequeType.Paid : ChequeType.Received;
        PartyTypeLabel = RbPaid.IsChecked == true ? "تأمین‌کننده" : "مشتری";
        PartyId = pid;
        ChequeNumber = TxtNumber.Text.Trim();
        BankName = TxtBank.Text.Trim();
        Amount = amount;
        DueDate = TxtDue.Text.Trim();
        Date = string.IsNullOrWhiteSpace(TxtDate.Text) ? TxtDue.Text.Trim() : TxtDate.Text.Trim();
        IssuedBy = string.IsNullOrWhiteSpace(TxtIssuedBy.Text) ? null : TxtIssuedBy.Text.Trim();
        Description = string.IsNullOrWhiteSpace(TxtDesc.Text) ? null : TxtDesc.Text.Trim();

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
