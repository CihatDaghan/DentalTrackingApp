using Dental.Integrations.Common;

namespace Dental.UnitTests.Integrations;

public sealed class IntegrationLogSanitizerTests
{
    [Fact]
    public void Sanitize_MasksTckn()
    {
        var result = IntegrationLogSanitizer.Sanitize("Hasta TCKN: 12345678901 ile kayitli.");

        Assert.Equal("Hasta TCKN: 12*******01 ile kayitli.", result);
    }

    [Theory]
    [InlineData("905551112233", "90********33")]
    [InlineData("+905551112233", "+9*********33")]
    [InlineData("05321234567", "05*******67")]
    [InlineData("5551112233", "55******33")]
    public void Sanitize_MasksTurkishPhoneFormats(string phone, string masked)
    {
        Assert.Equal($"Tel: {masked} kayitli", IntegrationLogSanitizer.Sanitize($"Tel: {phone} kayitli"));
    }

    [Fact]
    public void Sanitize_MasksEmail()
    {
        var result = IntegrationLogSanitizer.Sanitize("Alici: ayse.yilmaz@example.com adresine gonderildi.");

        Assert.Equal("Alici: a***@e*** adresine gonderildi.", result);
    }

    [Fact]
    public void Sanitize_MixedText_MasksAllPiiKeepsRest()
    {
        var input = "SMS to=905551112233 tckn=12345678901 email=ali@klinik.com status=OK";

        var result = IntegrationLogSanitizer.Sanitize(input);

        Assert.Equal("SMS to=90********33 tckn=12*******01 email=a***@k*** status=OK", result);
    }

    [Fact]
    public void Sanitize_DoesNotTouchShortNumbersOrDates()
    {
        var input = "Randevu 21.08.2026 14:00, oda 305, tutar 1500,50 TL";

        Assert.Equal(input, IntegrationLogSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", IntegrationLogSanitizer.Sanitize(null));
        Assert.Equal("", IntegrationLogSanitizer.Sanitize(""));
    }

    [Fact]
    public void MaskHelpers_MaskSingleValues()
    {
        Assert.Equal("12*******01", IntegrationLogSanitizer.MaskTckn("12345678901"));
        Assert.Equal("90********33", IntegrationLogSanitizer.MaskPhone("905551112233"));
        Assert.Equal("a***@e***", IntegrationLogSanitizer.MaskEmail("ayse@example.com"));
    }
}
