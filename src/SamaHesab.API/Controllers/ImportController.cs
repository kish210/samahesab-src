using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Import;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-IMPORT — مهاجرت از سایرِ برنامه‌ها (حساب‌فا/سپیدار/هلو/اکسلِ استاندارد) رویِ وب.
/// عیناً همان زیرساختِ دسکتاپ (DataImportViewModel) بازاستفاده می‌شود: فایلِ .xlsx آپلود
/// می‌شود، سرور با `IExcelImportService` (ClosedXML) سطرها را می‌خواند و به همان
/// Import*Commandهای Application می‌فرستد (idempotent بر اساسِ کد — تکراری‌ها رد می‌شوند).
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/import")]
public class ImportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExcelImportService _excel;
    public ImportController(IMediator mediator, IExcelImportService excel)
    { _mediator = mediator; _excel = excel; }

    /// <param name="entityType">customers | suppliers | persons | products</param>
    [HttpPost("{entityType}")]
    [RequestSizeLimit(20_000_000)]   // سقفِ ۲۰MB — کافی برایِ فایلِ اکسلِ اشخاص/کالا
    public async Task<IActionResult> Import(string entityType, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "فایلی ارسال نشده است." });
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "فقط فایلِ اکسل (.xlsx) پذیرفته می‌شود." });

        // ReadRows مسیرِ فایل می‌گیرد (نه استریم) — پس در یک فایلِ موقتِ منحصربه‌فرد ذخیره و بعد پاک می‌کنیم.
        var tmp = Path.Combine(Path.GetTempPath(), $"sh_import_{Guid.NewGuid():N}.xlsx");
        try
        {
            await using (var fs = System.IO.File.Create(tmp))
                await file.CopyToAsync(fs, ct);

            IReadOnlyList<IReadOnlyDictionary<string, string>> rows;
            try { rows = _excel.ReadRows(tmp); }
            catch (Exception ex) { return BadRequest(new { message = "خطا در خواندنِ فایلِ اکسل: " + ex.Message }); }

            if (rows.Count == 0)
                return BadRequest(new { message = "هیچ سطرِ داده‌ای یافت نشد (سطرِ اول باید سرستون باشد)." });

            ImportResult res = entityType.ToLowerInvariant() switch
            {
                "products" => await _mediator.Send(new ImportProductsCommand(rows), ct),
                "suppliers" => await _mediator.Send(new ImportSuppliersCommand(rows), ct),
                "persons" => await _mediator.Send(new ImportPersonsCommand(rows), ct),
                "customers" => await _mediator.Send(new ImportCustomersCommand(rows), ct),
                _ => throw new ArgumentException("invalid entity type"),
            };
            return Ok(res);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "نوعِ داده نامعتبر است (customers/suppliers/persons/products)." });
        }
        finally
        {
            try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { /* پاک‌سازیِ موقت — بی‌اهمیت */ }
        }
    }
}
