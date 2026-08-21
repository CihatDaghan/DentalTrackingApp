using Dental.Application.Abstractions;
using Dental.Integrations.Payments.Fake;
using Dental.Integrations.Sms.Fake;
using Dental.Integrations.WhatsApp.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dental.UnitTests.Integrations;

public sealed class FakeProvidersTests
{
    [Fact]
    public async Task FakeSmsProvider_AlwaysSucceeds_WithDeterministicId()
    {
        var provider = new FakeSmsProvider(NullLogger<FakeSmsProvider>.Instance);
        var message = new SmsMessage("905551112233", "Randevu hatirlatma", "KLINIK");

        var first = await provider.SendAsync(message);
        var second = await provider.SendAsync(message);
        var different = await provider.SendAsync(message with { Body = "Baska mesaj" });

        Assert.True(first.Success);
        Assert.StartsWith("fake-sms-", first.ProviderMessageId);
        Assert.Equal(first.ProviderMessageId, second.ProviderMessageId);
        Assert.NotEqual(first.ProviderMessageId, different.ProviderMessageId);
        Assert.Equal(1000m, await provider.GetBalanceAsync());
    }

    [Fact]
    public async Task FakeWhatsAppProvider_AlwaysSucceeds_WithDeterministicId()
    {
        var provider = new FakeWhatsAppProvider(NullLogger<FakeWhatsAppProvider>.Instance);
        var message = new WaTemplateMessage("905551112233", "randevu_hatirlatma", "tr", ["Ayşe", "14:00"]);

        var first = await provider.SendTemplateAsync(message);
        var second = await provider.SendTemplateAsync(message);

        Assert.True(first.Success);
        Assert.StartsWith("fake-wa-", first.ProviderMessageId);
        Assert.Equal(first.ProviderMessageId, second.ProviderMessageId);
    }

    [Fact]
    public async Task FakePaymentProvider_CheckoutThenVerify_SucceedsOnFirstCall()
    {
        var provider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance, "http://localhost:4200/dev/fake-payment");
        var request = new PaymentCheckoutRequest(
            ConversationId: "42",
            Amount: 1500.50m,
            Currency: "TRY",
            Description: "Kanal tedavisi",
            BuyerName: "Ayşe Yılmaz",
            BuyerEmail: "ayse@example.com",
            BuyerPhone: "905551112233",
            CallbackUrl: "https://klinik.local/api/payments/callback/fake");

        var checkout = await provider.CreateCheckoutAsync(request);

        Assert.Equal("fake-tok-42", checkout.ProviderToken);
        Assert.StartsWith("http://localhost:4200/dev/fake-payment?token=fake-tok-42", checkout.PaymentPageUrl);

        var verify = await provider.VerifyPaymentAsync(checkout.ProviderToken);

        Assert.Equal(PaymentVerifyStatus.Success, verify.Status);
        Assert.Equal("fake-pay-42", verify.ProviderPaymentId);
        Assert.Equal(1500.50m, verify.PaidAmount);
        Assert.NotNull(verify.RawJson);
        Assert.Null(verify.Error);

        // İkinci doğrulama da deterministik olarak aynı sonucu verir (idempotent callback testi).
        var verifyAgain = await provider.VerifyPaymentAsync(checkout.ProviderToken);
        Assert.Equal(PaymentVerifyStatus.Success, verifyAgain.Status);
        Assert.Equal("fake-pay-42", verifyAgain.ProviderPaymentId);
    }

    [Fact]
    public async Task FakePaymentProvider_UnknownToken_ReturnsFailure()
    {
        var provider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);

        var verify = await provider.VerifyPaymentAsync("olmayan-token");

        Assert.Equal(PaymentVerifyStatus.Failure, verify.Status);
        Assert.Contains("olmayan-token", verify.Error);
    }
}
