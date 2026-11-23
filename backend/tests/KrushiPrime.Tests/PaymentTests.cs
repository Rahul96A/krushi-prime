using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;

namespace KrushiPrime.Tests;

public class PaymentGatewayTests
{
    private const string NttSandboxKey = "A4476C2062FFA58980DC8F79EB6A799E"; // public NDPS sandbox key

    private const string Plain = """{"payInstrument":{"merchDetails":{"merchTxnId":"KP123"},"payDetails":{"totalAmount":"150.00","atomTxnId":"11000000123"},"responseDetails":{"statusCode":"OTS0000","description":"SUCCESS"}}}""";

    // Produced by an independent Python implementation (hashlib PBKDF2-SHA512 + cryptography AES-CBC) of the NDPS spec
    private const string Cipher = "582BAC98DB50B7B7465FEC80C84D6FA1E6A204F4B1819E493FDDB77259934E0B429360D8B9BAC2BD3AEEC800DF7A923BA3319EE0B146F5A8E20023051ADB196C69B24EDDFDA16E6BB1174106B44B164126DEF6F74A09944E9B2DDB334090D7A3CD895427FDDEF6AA9FE804A4DA096BBBAF7ACA4FB2D48EE6844997DFD6328AF43D2E9D5E26E262912E97E1DA8064F33C070C205E1117643A18FE01EDCA98F5FE3EEE4363A0FE0498CED8CD285D41C2B62A5DD3D1D033C4BEB8F1B44DA3B259C5";

    [Fact]
    public void Ntt_aes_matches_reference_implementation()
    {
        Assert.Equal(Cipher, AtomAes.Encrypt(Plain, NttSandboxKey));
        Assert.Equal(Plain, AtomAes.Decrypt(Cipher, NttSandboxKey));
    }

    [Fact]
    public void Ntt_response_is_parsed()
    {
        var r = NttDataGateway.ParseResponse(Cipher, NttSandboxKey);
        Assert.Equal("KP123", r.MerchTxnId);
        Assert.Equal(PaymentLinkStatus.Paid, r.Status);
        Assert.Equal(150m, r.Amount);
        Assert.Equal("11000000123", r.AtomTxnId);
    }

    [Fact]
    public void Cashfree_webhook_signature_is_verified()
    {
        const string body = """{"type":"PAYMENT_LINK_EVENT"}""";
        Assert.True(CashfreeGateway.VerifySignature(body, "1746427759733", "vuvuIpVURkblf8CUcVQCtqZMLIb78puxmZNfBR84C1A=", "test-secret"));
        Assert.False(CashfreeGateway.VerifySignature(body + " ", "1746427759733", "vuvuIpVURkblf8CUcVQCtqZMLIb78puxmZNfBR84C1A=", "test-secret"));
        Assert.False(CashfreeGateway.VerifySignature(body, "1746427759733", "vuvuIpVURkblf8CUcVQCtqZMLIb78puxmZNfBR84C1A=", "wrong"));
    }

    [Theory]
    [InlineData("PAID", PaymentLinkStatus.Paid)]
    [InlineData("PARTIALLY_PAID", PaymentLinkStatus.PartiallyPaid)]
    [InlineData("EXPIRED", PaymentLinkStatus.Expired)]
    [InlineData("ACTIVE", PaymentLinkStatus.Created)]
    public void Cashfree_status_mapping(string s, PaymentLinkStatus expected) => Assert.Equal(expected, CashfreeGateway.MapStatus(s));

    [Fact]
    public void Secrets_are_encrypted_at_rest()
    {
        var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:EncryptionKey"] = "unit-test-key" }).Build();
        var p = new SecretProtector(cfg);
        var enc = p.Protect("cfsk_ma_test_123456");
        Assert.NotEqual("cfsk_ma_test_123456", enc);
        Assert.Equal("cfsk_ma_test_123456", p.Unprotect(enc));
        Assert.Equal("••••3456", SecretProtector.Mask("cfsk_ma_test_123456"));
    }
}
