using Dental.Application.Abstractions;
using Dental.Integrations.Payments.Iyzico;

namespace Dental.UnitTests.Integrations;

/// <summary>
/// Iyzipay SDK'sı HTTP çağrılarını statik metodlar içinde yaptığından burada yalnız
/// ayar/istek kurulumları test edilir; canlı sandbox doğrulaması G aşamasında manueldir.
/// </summary>
public sealed class IyzicoPaymentProviderTests
{
    private static PaymentCheckoutRequest SampleRequest(decimal amount = 1500.50m) => new(
        ConversationId: "42",
        Amount: amount,
        Currency: "TRY",
        Description: "Kanal tedavisi",
        BuyerName: "Ayşe Yılmaz",
        BuyerEmail: "ayse@example.com",
        BuyerPhone: "+905551112233",
        CallbackUrl: "https://klinik.local/api/payments/callback/iyzico");

    [Fact]
    public void BuildOptions_MapsSettings()
    {
        var options = IyzicoPaymentProvider.BuildOptions(new IyzicoSettings
        {
            ApiKey = "sandbox-key",
            SecretKey = "sandbox-secret",
            BaseUrl = "https://sandbox-api.iyzipay.com",
        });

        Assert.Equal("sandbox-key", options.ApiKey);
        Assert.Equal("sandbox-secret", options.SecretKey);
        Assert.Equal("https://sandbox-api.iyzipay.com", options.BaseUrl);
    }

    [Fact]
    public void BuildCheckoutRequest_MapsCoreFields()
    {
        var request = IyzicoPaymentProvider.BuildCheckoutRequest(SampleRequest());

        Assert.Equal("42", request.ConversationId);
        Assert.Equal("42", request.BasketId);
        Assert.Equal("1500.5", request.Price);
        Assert.Equal("1500.5", request.PaidPrice);
        Assert.Equal("TRY", request.Currency);
        Assert.Equal("PRODUCT", request.PaymentGroup);
        Assert.Equal("https://klinik.local/api/payments/callback/iyzico", request.CallbackUrl);

        Assert.Equal("Ayşe", request.Buyer.Name);
        Assert.Equal("Yılmaz", request.Buyer.Surname);
        Assert.Equal("ayse@example.com", request.Buyer.Email);
        Assert.Equal("+905551112233", request.Buyer.GsmNumber);
        Assert.Equal("11111111111", request.Buyer.IdentityNumber);

        var item = Assert.Single(request.BasketItems);
        Assert.Equal("VIRTUAL", item.ItemType);
        Assert.Equal("Kanal tedavisi", item.Name);
        Assert.Equal("1500.5", item.Price);
    }

    [Fact]
    public void BuildCheckoutRequest_SingleWordName_UsesPlaceholderSurname()
    {
        var request = IyzicoPaymentProvider.BuildCheckoutRequest(
            SampleRequest() with { BuyerName = "Ayşe", BuyerEmail = null });

        Assert.Equal("Ayşe", request.Buyer.Name);
        Assert.Equal("-", request.Buyer.Surname);
        // e-posta zorunlu alan; verilmediyse yer tutucu kullanılır.
        Assert.False(string.IsNullOrWhiteSpace(request.Buyer.Email));
    }

    [Theory]
    [InlineData("150", "150.0")]
    [InlineData("150.5", "150.5")]
    [InlineData("150.55", "150.55")]
    [InlineData("0.99", "0.99")]
    public void FormatAmount_UsesInvariantDotDecimal(string amount, string expected)
    {
        Assert.Equal(expected, IyzicoPaymentProvider.FormatAmount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
