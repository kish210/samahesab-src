using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Licensing;
using SamaHesab.Application.Security.Commands;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// U-SUPPORT-RESET — وقتی کاربر هم رمز و هم کدِ بازیابیِ محلی را گم کرده: پشتیبانی با کلیدِ
/// خصوصیِ RSA (که هرگز در کلاینت/این مخزن نیست) یک توکنِ کوتاه‌مدتِ مخصوصِ Fingerprintِ همان
/// دستگاه امضا می‌کند. این تست‌ها کلیدِ آزمایشیِ خودشان را می‌سازند (نه کلیدِ واقعیِ وندور).
/// </summary>
public class SupportResetTokenTests
{
    private static (string pub, string priv) NewKeys()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    [Fact]
    public void Sign_Then_Verify_Succeeds_For_Same_Fingerprint()
    {
        var (pub, priv) = NewKeys();
        var now = DateTime.UtcNow;
        var token = new SupportResetToken("FP-ABC-123", now, now.AddDays(2));
        var doc = new SupportResetTokenDocument(token, SupportResetTokenSigner.Sign(token, priv));

        Assert.True(SupportResetTokenSigner.Verify(doc, "FP-ABC-123", now.AddHours(1), pub));
    }

    [Fact]
    public void Verify_Fails_For_Different_Machine_Fingerprint()
    {
        var (pub, priv) = NewKeys();
        var now = DateTime.UtcNow;
        var token = new SupportResetToken("FP-ABC-123", now, now.AddDays(2));
        var doc = new SupportResetTokenDocument(token, SupportResetTokenSigner.Sign(token, priv));

        Assert.False(SupportResetTokenSigner.Verify(doc, "FP-XYZ-999", now.AddHours(1), pub));
    }

    [Fact]
    public void Verify_Fails_After_Expiry()
    {
        var (pub, priv) = NewKeys();
        var now = DateTime.UtcNow;
        var token = new SupportResetToken("FP-ABC-123", now.AddDays(-3), now.AddDays(-1));
        var doc = new SupportResetTokenDocument(token, SupportResetTokenSigner.Sign(token, priv));

        Assert.False(SupportResetTokenSigner.Verify(doc, "FP-ABC-123", now, pub));
    }

    [Fact]
    public void Verify_Fails_For_Tampered_Fingerprint_In_Payload()
    {
        var (pub, priv) = NewKeys();
        var now = DateTime.UtcNow;
        var token = new SupportResetToken("FP-ABC-123", now, now.AddDays(2));
        var sig = SupportResetTokenSigner.Sign(token, priv);
        // دستکاری: توکن ادعا می‌کند مخصوصِ FP-EVIL است ولی امضا هنوز مالِ FP-ABC-123 است.
        var tampered = new SupportResetToken("FP-EVIL", now, now.AddDays(2));
        var doc = new SupportResetTokenDocument(tampered, sig);

        Assert.False(SupportResetTokenSigner.Verify(doc, "FP-EVIL", now, pub));
    }

    [Fact]
    public void Verify_Fails_With_Wrong_Public_Key()
    {
        var (_, priv) = NewKeys();
        var (otherPub, _) = NewKeys();
        var now = DateTime.UtcNow;
        var token = new SupportResetToken("FP-ABC-123", now, now.AddDays(2));
        var doc = new SupportResetTokenDocument(token, SupportResetTokenSigner.Sign(token, priv));

        Assert.False(SupportResetTokenSigner.Verify(doc, "FP-ABC-123", now, otherPub));
    }

    [Fact]
    public void FromCode_Round_Trips_ToCode()
    {
        var now = DateTime.UtcNow;
        var token = new SupportResetToken("FP-ABC-123", now, now.AddDays(2));
        var doc = new SupportResetTokenDocument(token, "fake-signature");

        var decoded = SupportResetTokenDocument.FromCode(doc.ToCode());

        Assert.NotNull(decoded);
        Assert.Equal("FP-ABC-123", decoded!.Token.MachineFingerprint);
        Assert.Equal("fake-signature", decoded.Signature);
    }

    [Fact]
    public void FromCode_Returns_Null_For_Garbage_Input()
    {
        Assert.Null(SupportResetTokenDocument.FromCode("not-valid-base64!!"));
        Assert.Null(SupportResetTokenDocument.FromCode(""));
        Assert.Null(SupportResetTokenDocument.FromCode(null));
    }

    // ── ResetPasswordWithSupportTokenCommand ──

    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static void SetId(T e, int value)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop != null) prop.SetValue(e, Convert.ChangeType(value, prop.PropertyType));
        }
        public Task AddAsync(T e, CancellationToken ct = default)
        { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(e => (int)(typeof(T).GetProperty("Id")!.GetValue(e) ?? 0) == id));
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { foreach (var x in es.ToList()) Items.Remove(x); }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeFingerprintProvider : IMachineFingerprintProvider
    {
        public string Fingerprint = "THIS-MACHINE";
        public string GetFingerprint() => Fingerprint;
    }

    /// <summary>این تست‌ها با کلیدِ عمومیِ واقعیِ محصول (LicensePublicKey.Pem) کار نمی‌کنند چون
    /// کلیدِ خصوصیِ متناظر را نداریم؛ به‌جایش مستقیماً نتیجهٔ SupportResetTokenSigner.Verify را
    /// روی همان کلیدِ آزمایشی بررسی می‌کنیم (تستِ منطقِ Handler، نه کلیدِ واقعی).</summary>
    [Fact]
    public async Task ResetPasswordWithSupportTokenCommand_Fails_Gracefully_For_Invalid_Code()
    {
        var users = new FakeRepo<User>();
        await users.AddAsync(User.Create(1, null, "admin", "x", "y", "مدیر"));
        var handler = new ResetPasswordWithSupportTokenCommandHandler(users, new FakeUow(), new FakeFingerprintProvider());

        var res = await handler.Handle(new ResetPasswordWithSupportTokenCommand(1, "admin", "garbage-not-a-token", "NewPass123"), default);

        Assert.False(res.Succeeded);
    }
}
