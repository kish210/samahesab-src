using System.Security.Cryptography;
using SamaHesab.Application.Licensing;

// ─────────────────────────────────────────────────────────────────────────────
// فاز ۱۲ P-G7 — ابزارِ صدورِ لایسنسِ وندور (آفلاین، RSA).
//   keygen                       → ساختِ جفت‌کلید (private نزدِ وندور، public برای جای‌گذاری در کلاینت)
//   sign --fp <X> --company "<Y>" [--national <Z>] --tier Professional --days 365 [--out file.lic]
// کلیدِ خصوصی در: %USERPROFILE%\.samahesab\license_private.pem  (هرگز در مخزن/کلاینت)
// ─────────────────────────────────────────────────────────────────────────────

string KeyDir() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".samahesab");
string PrivPath() => Path.Combine(KeyDir(), "license_private.pem");

string? Arg(string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], "--" + name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
    return null;
}

if (args.Length == 0) { Help(); return 1; }

switch (args[0].ToLowerInvariant())
{
    case "keygen":
    {
        using var rsa = RSA.Create(2048);
        Directory.CreateDirectory(KeyDir());
        File.WriteAllText(PrivPath(), rsa.ExportPkcs8PrivateKeyPem());
        Console.WriteLine($"✔ کلیدِ خصوصی ذخیره شد (وندور): {PrivPath()}");
        Console.WriteLine();
        Console.WriteLine("کلیدِ عمومی — این را در LicensePublicKey.Pem کلاینت جای‌گذاری کنید:");
        Console.WriteLine(rsa.ExportSubjectPublicKeyInfoPem());
        return 0;
    }

    case "sign":
    {
        var fp = Arg("fp") ?? Arg("fingerprint");
        var company = Arg("company");
        if (string.IsNullOrWhiteSpace(fp) || string.IsNullOrWhiteSpace(company))
        { Console.Error.WriteLine("خطا: --fp و --company الزامی‌اند."); return 1; }

        if (!File.Exists(PrivPath()))
        { Console.Error.WriteLine($"خطا: کلیدِ خصوصی یافت نشد ({PrivPath()}). ابتدا `keygen` را اجرا کنید."); return 1; }

        var tier = Enum.TryParse<LicenseTier>(Arg("tier"), true, out var t) ? t : LicenseTier.Starter;
        var days = int.TryParse(Arg("days"), out var d) ? d : 365;
        var (maxBranches, maxUsers) = LicenseLimits.For(tier);
        var now = DateTime.UtcNow;

        var info = new LicenseInfo(company!.Trim(), Arg("national"), fp!.Trim(), tier,
            now, now.AddDays(days), maxBranches, maxUsers);

        var sig = RsaLicense.Sign(info, File.ReadAllText(PrivPath()));
        var doc = new LicenseDocument(info, sig);

        var outPath = Arg("out") ?? $"{Sanitize(company!)}.lic";
        File.WriteAllText(outPath, doc.ToJson(), new System.Text.UTF8Encoding(false));
        Console.WriteLine($"✔ لایسنس صادر شد: {outPath}");
        Console.WriteLine($"  شرکت: {company} · رده: {tier} · انقضا: {info.ExpiresUtc:yyyy-MM-dd} · شعبه≤{maxBranches} کاربر≤{maxUsers}");
        return 0;
    }

    case "verify":   // سلامت‌سنجی: آیا فایلِ لایسنس با کلیدِ عمومیِ embed‌شدهٔ کلاینت معتبر است؟
    {
        var file = Arg("file");
        var fp = Arg("fp") ?? Arg("fingerprint");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        { Console.Error.WriteLine("خطا: --file <path> الزامی و باید موجود باشد."); return 1; }

        var doc = LicenseDocument.FromJson(File.ReadAllText(file));
        var res = new LicenseValidator(LicensePublicKey.Pem)
            .Validate(doc, fp ?? doc?.License.MachineFingerprint ?? "", DateTime.UtcNow);
        Console.WriteLine($"وضعیت: {res.Status} — {res.Message}");
        return res.IsValid ? 0 : 2;
    }

    default: Help(); return 1;
}

void Help()
{
    Console.WriteLine("SamaHesab.LicenseTool — صدورِ لایسنسِ وندور (P-G7)");
    Console.WriteLine("  keygen");
    Console.WriteLine("  sign --fp <fingerprint> --company \"<name>\" [--national <id>] --tier <Starter|Professional|Enterprise> --days <n> [--out <file.lic>]");
}

static string Sanitize(string s) => string.Join("_", s.Trim().Split(Path.GetInvalidFileNameChars()));
