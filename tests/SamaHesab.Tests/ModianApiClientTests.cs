using System.Net;
using System.Net.Http;
using SamaHesab.Modules.TaxInvoicing.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-2 (سامانهٔ مودیان) — تستِ ModianApiClient با HttpMessageHandlerِ جعلی (بدونِ شبکهٔ
/// واقعی/Sandbox). فقط سریال‌سازیِ درخواست/تجزیهٔ پاسخ و مسیرِ try/catch→Result را می‌سنجد.</summary>
public class ModianApiClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? LastRequest;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task GetNonce_Returns_Value_From_Response_Body()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"nonce\":\"abc123\"}"));
        var client = new ModianApiClient(new HttpClient(handler));

        var res = await client.GetNonceAsync(useSandbox: true);

        Assert.True(res.Succeeded);
        Assert.Equal("abc123", res.Value);
        Assert.Contains("sandboxrc.tax.gov.ir", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetNonce_Fails_On_NonSuccess_StatusCode()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.InternalServerError, "boom"));
        var client = new ModianApiClient(new HttpClient(handler));

        var res = await client.GetNonceAsync(useSandbox: true);

        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task GetNonce_Uses_Production_Url_When_Not_Sandbox()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"nonce\":\"x\"}"));
        var client = new ModianApiClient(new HttpClient(handler));

        await client.GetNonceAsync(useSandbox: false);

        Assert.Contains("tp.tax.gov.ir", handler.LastRequest!.RequestUri!.ToString());
        Assert.DoesNotContain("sandbox", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SubmitInvoice_Returns_ReferenceNumber_On_Success()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"referenceNumber\":\"REF-999\"}"));
        var client = new ModianApiClient(new HttpClient(handler));

        var res = await client.SubmitInvoiceAsync("dummy.jwe.token", useSandbox: true);

        Assert.True(res.Succeeded);
        Assert.Equal("REF-999", res.Value!.ReferenceNumber);
    }

    [Fact]
    public async Task SubmitInvoice_Fails_And_Includes_Server_Body_On_Rejection()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid uid\"}"));
        var client = new ModianApiClient(new HttpClient(handler));

        var res = await client.SubmitInvoiceAsync("dummy.jwe.token", useSandbox: true);

        Assert.False(res.Succeeded);
        Assert.Contains("invalid uid", res.ErrorMessage);
    }

    [Fact]
    public async Task InquiryByReferenceNumber_Parses_Status_And_Uid()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK,
            "{\"status\":\"Accepted\",\"uid\":\"1234567890123456789012\",\"description\":\"ok\"}"));
        var client = new ModianApiClient(new HttpClient(handler));

        var res = await client.InquiryByReferenceNumberAsync("REF-1", useSandbox: true);

        Assert.True(res.Succeeded);
        Assert.Equal("Accepted", res.Value!.Status);
        Assert.Equal("1234567890123456789012", res.Value.UniqueTaxId);
    }

    [Fact]
    public async Task GetServerPublicKeyPem_Fails_When_Response_Missing_Key()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var client = new ModianApiClient(new HttpClient(handler));

        var res = await client.GetServerPublicKeyPemAsync(useSandbox: true);

        Assert.False(res.Succeeded);
    }
}
