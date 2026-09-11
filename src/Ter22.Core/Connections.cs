using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Ter22.Core;

public static class Connections
{
    public static async Task<SslStream> SecureClientAsync(Stream transport, string pin, CancellationToken ct)
    {
        var ssl = new SslStream(transport, false, (_, cert, _, _) => Identity.Matches(cert, pin));
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "ter22-remote", EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                AllowRenegotiation = false, CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, timeout.Token);
            return ssl;
        }
        catch { ssl.Dispose(); throw; }
    }

    public static async Task<SslStream> SecureServerAsync(Stream transport, Identity identity, CancellationToken ct)
    {
        var ssl = new SslStream(transport, false);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = identity.Certificate, ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13, AllowRenegotiation = false,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, timeout.Token);
            return ssl;
        }
        catch { ssl.Dispose(); throw; }
    }

    public static async Task<Stream> DialAsync(string host, int port, CancellationToken ct)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            await socket.ConnectAsync(host, port, timeout.Token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch { socket.Dispose(); throw; }
    }

    public static async Task<Stream> RelayAsync(RelayAddress address, string route, bool host, CancellationToken ct)
    {
        address.Validate();
        Stream transport = await DialAsync(address.Host, address.Port, ct);
        var outer = await SecureClientAsync(transport, address.Pin, ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(host ? 115 : 20));
            await Wire.WriteAsync(outer, Kind.RelayHello, Wire.Json(new RelayHello(Wire.Version, address.Key, route, host)), timeout.Token);
            var response = await Wire.ReadAsync(outer, timeout.Token, 8192);
            if (response.Kind != Kind.RelayReady) throw new IOException("Calculatorul nu este disponibil la releu.");
            return outer; // The caller wraps this in a second, end-to-end TLS connection.
        }
        catch { outer.Dispose(); throw; }
    }

    public static async Task<SslStream> ConnectViewerAsync(Invitation invitation, CancellationToken ct)
    {
        Stream transport = invitation.Relay is null
            ? await DialAsync(invitation.Host, invitation.Port, ct)
            : await RelayAsync(invitation.Relay, invitation.Route, false, ct);
        var ssl = await SecureClientAsync(transport, invitation.Pin, ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await Wire.WriteAsync(ssl, Kind.Auth, Wire.Json(new Authentication(Wire.Version, invitation.Token)), timeout.Token);
            var response = await Wire.ReadAsync(ssl, timeout.Token, 8192);
            if (response.Kind != Kind.Accepted) throw new AuthenticationException("Conexiunea nu a fost autorizată.");
            return ssl;
        }
        catch { ssl.Dispose(); throw; }
    }

    public static async Task<bool> AuthenticateViewerAsync(Stream ssl, string token, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var packet = await Wire.ReadAsync(ssl, timeout.Token, 8192);
        if (packet.Kind != Kind.Auth) return false;
        var auth = packet.Json<Authentication>();
        return auth.Version == Wire.Version && Identity.EqualSecret(auth.Token, token);
    }
}
