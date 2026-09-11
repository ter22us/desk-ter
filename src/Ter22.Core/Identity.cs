using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Ter22.Core;

public sealed class Identity : IDisposable
{
    public X509Certificate2 Certificate { get; }
    public string Pin => Convert.ToHexString(SHA256.HashData(Certificate.RawData));
    public Identity(X509Certificate2 certificate) => Certificate = certificate;
    public static Identity Create()
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=ter22-remote", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        // Schannel uses a user key container on Windows. Without PersistKeySet,
        // the imported container is owned by the certificate and removed on disposal.
        var storage = X509KeyStorageFlags.Exportable | (OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.UserKeySet : X509KeyStorageFlags.EphemeralKeySet);
        return new Identity(X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, storage));
    }
    public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static bool ValidSecret(string? value)
    {
        if (value is null || value.Length != 44) return false;
        return Convert.TryFromBase64String(value, new byte[32], out int n) && n == 32;
    }
    public static bool EqualSecret(string? received, string expected)
    {
        if (!ValidSecret(received) || !ValidSecret(expected)) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(received!), Convert.FromBase64String(expected));
    }
    public static bool ValidPin(string? pin) => pin is { Length: 64 } && pin.All(Uri.IsHexDigit);
    public static bool Matches(X509Certificate? cert, string pin)
    {
        if (cert is null || !ValidPin(pin)) return false;
        using var parsed = X509CertificateLoader.LoadCertificate(cert.GetRawCertData());
        return DateTime.UtcNow >= parsed.NotBefore.ToUniversalTime() && DateTime.UtcNow <= parsed.NotAfter.ToUniversalTime()
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(parsed.RawData), Convert.FromHexString(pin));
    }
    public void Dispose() => Certificate.Dispose();
}

public sealed record RelayAddress(string Host, int Port, string Pin, string Key)
{
    public void Validate()
    {
        Codes.ValidateHost(Host, Port);
        if (!Identity.ValidPin(Pin) || !Identity.ValidSecret(Key)) throw new InvalidDataException("Cod releu invalid.");
    }
    public string ToCode() => Codes.Encode("TRR1:", this);
    public static RelayAddress Parse(string code)
    {
        var result = Codes.Decode<RelayAddress>("TRR1:", code);
        result.Validate(); return result;
    }
}

public sealed record Invitation(int Version, string Host, int Port, string Pin, string Token,
    string Route, RelayAddress? Relay)
{
    public string ToCode() => Codes.Encode("TRC1:", this);
    public static Invitation Parse(string code)
    {
        var result = Codes.Decode<Invitation>("TRC1:", code);
        if (result.Version != Wire.Version || !Identity.ValidPin(result.Pin) || !Identity.ValidSecret(result.Token)
            || !Guid.TryParseExact(result.Route, "N", out _)) throw new InvalidDataException("Cod conexiune invalid.");
        if (result.Relay is null) Codes.ValidateHost(result.Host, result.Port);
        else result.Relay.Validate();
        return result;
    }
}

public static class Codes
{
    public static void ValidateHost(string? host, int port)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || port is < 1 or > 65535
            || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            throw new InvalidDataException("Adresa trebuie să fie un IP sau un nume DNS, fără protocol sau port.");
    }
    public static string Encode<T>(string prefix, T value) => prefix + Convert.ToBase64String(Wire.Json(value));
    public static T Decode<T>(string prefix, string code)
    {
        code = code.Trim();
        if (code.Length > 8192 || !code.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Cod necunoscut.");
        try
        {
            return JsonSerializer.Deserialize<T>(Convert.FromBase64String(code[prefix.Length..]), Wire.JsonOptions)
                ?? throw new InvalidDataException("Cod incomplet.");
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        { throw new InvalidDataException("Cod deteriorat.", ex); }
    }
}
