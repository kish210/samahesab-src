using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SamaHesab.SellerWeb;
using SamaHesab.SellerWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClientِ بدونِ BaseAddress — آدرسِ سرور را کاربر در صفحهٔ ورود می‌دهد (مثلِ کلاینتِ دسکتاپ).
builder.Services.AddScoped(_ => new HttpClient());
builder.Services.AddSingleton<SellerApi>();

await builder.Build().RunAsync();
