namespace SamaHesab.WPF.Services;

/// <summary>
/// Connection settings for the POS / restaurant kiosk clients, which talk to the
/// central server over the Web API (HTTP) instead of the database directly.
/// </summary>
public class ApiSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5080";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
    public int CustomerId { get; set; } = 1;   // walk-in customer
    public int WarehouseId { get; set; } = 1;   // default warehouse
}
