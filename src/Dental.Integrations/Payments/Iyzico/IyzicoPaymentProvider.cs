using System.Globalization;
using System.Text.Json;
using Dental.Application.Abstractions;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Payments.Iyzico;

/// <summary>
/// iyzico CheckoutForm sürücüsü (resmî Iyzipay SDK). Akış: CreateCheckoutAsync ile hosted ödeme
/// sayfası açılır (3DS sağlayıcıda, PCI kapsamı minimal); callback geldiğinde VerifyPaymentAsync
/// ile sunucudan yeniden doğrulanır — callback verisine asla tek başına güvenilmez.
/// SDK 2.1.x async-native'dir (Task döner); Task.Run sarmalaması yoktur. SDK CancellationToken
/// almadığından ct yalnız çağrı öncesi kontrol edilir.
/// </summary>
public sealed class IyzicoPaymentProvider(IyzicoSettings settings, ILogger<IyzicoPaymentProvider> logger) : IPaymentProvider
{
    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var sdkRequest = BuildCheckoutRequest(request);

        CheckoutFormInitialize init;
        try
        {
            init = await CheckoutFormInitialize.Create(sdkRequest, BuildOptions(settings)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PaymentProviderException($"iyzico checkout isteği başarısız: {ex.Message}", ex);
        }

        if (!string.Equals(init.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("iyzico checkout başlatılamadı. ConversationId={ConversationId} ErrorCode={ErrorCode}",
                request.ConversationId, init.ErrorCode);
            throw new PaymentProviderException($"iyzico checkout başlatılamadı: {init.ErrorCode} {init.ErrorMessage}");
        }

        logger.LogInformation("iyzico checkout oluşturuldu. ConversationId={ConversationId} Token={Token}",
            request.ConversationId, init.Token);
        return new PaymentCheckoutResult(init.Token, init.PaymentPageUrl);
    }

    public async Task<PaymentVerifyResult> VerifyPaymentAsync(string providerToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var retrieveRequest = new RetrieveCheckoutFormRequest { Token = providerToken, Locale = Locale.TR.ToString() };

        CheckoutForm form;
        try
        {
            form = await CheckoutForm.Retrieve(retrieveRequest, BuildOptions(settings)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PaymentProviderException($"iyzico ödeme doğrulama isteği başarısız: {ex.Message}", ex);
        }

        var rawJson = JsonSerializer.Serialize(form);

        if (!string.Equals(form.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentVerifyResult(
                PaymentVerifyStatus.Failure,
                ProviderPaymentId: form.PaymentId,
                RawJson: rawJson,
                Error: $"{form.ErrorCode} {form.ErrorMessage}".Trim());
        }

        decimal? paidAmount = decimal.TryParse(form.PaidPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var p)
            ? p
            : null;

        var status = form.PaymentStatus?.ToUpperInvariant() switch
        {
            "SUCCESS" => PaymentVerifyStatus.Success,
            "FAILURE" => PaymentVerifyStatus.Failure,
            // INIT_THREEDS / CALLBACK_THREEDS / BKM_POS_SELECTED vb. ara durumlar
            _ => PaymentVerifyStatus.Pending,
        };

        return new PaymentVerifyResult(
            status,
            ProviderPaymentId: form.PaymentId,
            PaidAmount: paidAmount,
            RawJson: rawJson,
            Error: status == PaymentVerifyStatus.Failure ? $"{form.ErrorCode} {form.ErrorMessage}".Trim() : null);
    }

    internal static Options BuildOptions(IyzicoSettings settings) => new()
    {
        ApiKey = settings.ApiKey,
        SecretKey = settings.SecretKey,
        BaseUrl = settings.BaseUrl,
    };

    internal static CreateCheckoutFormInitializeRequest BuildCheckoutRequest(PaymentCheckoutRequest request)
    {
        var amount = FormatAmount(request.Amount);
        var (firstName, lastName) = SplitName(request.BuyerName);

        return new CreateCheckoutFormInitializeRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = request.ConversationId,
            Price = amount,
            PaidPrice = amount,
            Currency = request.Currency,
            BasketId = request.ConversationId,
            PaymentGroup = "PRODUCT",
            CallbackUrl = request.CallbackUrl,
            // Ödeme linki akışında hastadan adres/TCKN toplanmaz; iyzico'nun zorunlu
            // buyer alanları yer tutucuyla geçilir (sağlık verisi sağlayıcıya taşınmaz).
            Buyer = new Buyer
            {
                Id = request.ConversationId,
                Name = firstName,
                Surname = lastName,
                IdentityNumber = "11111111111",
                Email = string.IsNullOrWhiteSpace(request.BuyerEmail) ? "odeme@klinik.local" : request.BuyerEmail,
                GsmNumber = request.BuyerPhone,
                RegistrationAddress = "Adres bilgisi alinmadi",
                City = "Istanbul",
                Country = "Turkey",
            },
            BillingAddress = new Address
            {
                ContactName = request.BuyerName,
                Description = "Adres bilgisi alinmadi",
                City = "Istanbul",
                Country = "Turkey",
            },
            BasketItems =
            [
                new BasketItem
                {
                    Id = request.ConversationId,
                    Name = string.IsNullOrWhiteSpace(request.Description) ? "Saglik hizmeti" : request.Description,
                    Category1 = "Saglik Hizmeti",
                    ItemType = "VIRTUAL",
                    Price = amount,
                },
            ],
        };
    }

    /// <summary>iyzico fiyat biçimi: nokta ondalıklı, en az bir ondalık basamak (örn. "150.0", "150.5").</summary>
    internal static string FormatAmount(decimal amount)
        => amount.ToString("0.0#", CultureInfo.InvariantCulture);

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var trimmed = (fullName ?? "").Trim();
        var idx = trimmed.LastIndexOf(' ');
        return idx <= 0
            ? (string.IsNullOrEmpty(trimmed) ? "-" : trimmed, "-")
            : (trimmed[..idx].Trim(), trimmed[(idx + 1)..]);
    }
}
