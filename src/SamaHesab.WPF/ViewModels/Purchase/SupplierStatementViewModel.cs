using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.CRM.Queries;        // StatementRow + GetSuppliersQuery
using SamaHesab.Application.Purchase.Queries;   // GetSupplierStatementQuery
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Purchase;

/// <summary>
/// C2-C — صورت‌حساب/ماندهٔ تأمین‌کننده. 🏛️ الگوی API-only: دادهٔ صورت‌حساب و فهرستِ تأمین‌کننده
/// از API (کلاینت) یا Application (دسکتاپ)؛ بدونِ ریپازیتوریِ مستقیم.
/// </summary>
public partial class SupplierStatementViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    public ObservableCollection<SupplierOption> Suppliers { get; } = new();
    public ObservableCollection<StatementRow> Rows { get; } = new();

    [ObservableProperty] private int? _selectedSupplierId;
    [ObservableProperty] private string? _fromDate;
    [ObservableProperty] private string? _toDate;
    [ObservableProperty] private string _supplierName = string.Empty;
    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private decimal _closingBalance;
    [ObservableProperty] private bool _hasData;

    public SupplierStatementViewModel(IMediator mediator, ApiClient api,
        IDialogService d, INavigationService n) : base(d, n)
    { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        Suppliers.Clear();
        if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            foreach (var s in await _api.GetSuppliersAsync())
                Suppliers.Add(new SupplierOption(s.Id, s.Name));
        else
            foreach (var s in await _mediator.Send(new GetSuppliersQuery()))
                Suppliers.Add(new SupplierOption(s.Id, s.Name));
    }

    partial void OnSelectedSupplierIdChanged(int? value) => _ = RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        if (SelectedSupplierId is not int id) return;
        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(new GetSupplierStatementQuery(id, NullIfEmpty(FromDate), NullIfEmpty(ToDate)));
            Rows.Clear();
            if (!result.Succeeded || result.Value is null)
            {
                HasData = false;
                await _dialogService.ShowErrorAsync(result.ErrorMessage ?? "خطا در تهیهٔ صورت‌حساب.");
                return;
            }
            var st = result.Value;
            SupplierName = st.SupplierName;
            foreach (var r in st.Rows) Rows.Add(r);
            TotalDebit = st.TotalDebit; TotalCredit = st.TotalCredit; ClosingBalance = st.ClosingBalance;
            HasData = true;
        }, "در حال تهیهٔ صورت‌حساب...");
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public record SupplierOption(int Id, string Name);
