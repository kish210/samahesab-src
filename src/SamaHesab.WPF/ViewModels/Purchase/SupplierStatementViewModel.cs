using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;        // StatementRow (مشترک)
using SamaHesab.Application.Purchase.Queries;   // GetSupplierStatementQuery
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Purchase;

/// <summary>
/// C2-C — صورت‌حساب/ماندهٔ تأمین‌کننده: انتخابِ تأمین‌کننده + بازهٔ تاریخ → تراکنش‌ها با ماندهٔ غلتان.
/// بک‌اندِ `GetSupplierStatementQuery` (فاکتور خرید/مرجوعی/پرداخت) از قبل آماده بود؛ این UIِ آن است.
/// </summary>
public partial class SupplierStatementViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<Supplier> _supplierRepo;

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

    public SupplierStatementViewModel(IMediator mediator, ICurrentUserService currentUser,
        IRepository<Supplier> supplierRepo, IDialogService d, INavigationService n) : base(d, n)
    { _mediator = mediator; _currentUser = currentUser; _supplierRepo = supplierRepo; }

    public override async Task LoadAsync()
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var all = await _supplierRepo.FindAsync(s => s.CompanyId == companyId && s.IsActive);
        Suppliers.Clear();
        foreach (var s in all.OrderBy(s => s.FullName))
            Suppliers.Add(new SupplierOption(s.Id, s.FullName ?? ""));
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
