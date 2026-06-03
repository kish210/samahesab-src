namespace SamaHesab.WPF.Services;

/// <summary>
/// Implemented by ViewModels that need the navigation parameter (e.g. open an
/// existing record by id). Called by the shell after LoadAsync.
/// </summary>
public interface INavigationAware
{
    Task OnNavigatedToAsync(object? parameter);
}
