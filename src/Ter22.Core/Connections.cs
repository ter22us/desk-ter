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

    public static async Task<SslStream> ConnectViewerAsync(Invitation invitation, CancellationToken ct,
        Action<string>? progress = null)
    {
        invitation.Validate();
        SslStream ssl;
        if (invitation.Relay is null) ssl = await ConnectDirectAsync(invitation, ct, progress);
        else
        {
            progress?.Invoke($"Conectare la releul {invitation.Relay.Host}:{invitation.Relay.Port}.");
            Stream transport;
            try { transport = await RelayAsync(invitation.Relay, invitation.Route, false, ct); }
            catch (Exception ex) when (IsConnectionError(ex) && !ct.IsCancellationRequested)
            { throw new ConnectionFailureException(ConnectionStage.Relay, "Releul nu a putut pune calculatoarele în legătură. Verifică disponibilitatea lui și pornește accesul pe calculatorul controlat.", ex); }
            progress?.Invoke("Releu conectat. Verific certificatul calculatorului controlat.");
            try { ssl = await SecureClientAsync(transport, invitation.Pin, ct); }
            catch (Exception ex) when (IsConnectionError(ex) && !ct.IsCancellationRequested)
            { throw new ConnectionFailureException(ConnectionStage.Tls, "Negocierea TLS cu calculatorul controlat a eșuat. Copiază codul său actual.", ex); }
        }
        try
        {
            progress?.Invoke("TLS verificat. Trimit cererea de acces și aștept aprobarea pe calculatorul controlat.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await Wire.WriteAsync(ssl, Kind.Auth, Wire.Json(new Authentication(Wire.Version, invitation.Token)), timeout.Token);
            var response = await Wire.ReadAsync(ssl, timeout.Token, 8192);
            if (response.Kind == Kind.Error)
                throw new ConnectionFailureException(ConnectionStage.Approval, response.Json<ConnectionRejection>().Reason switch
                {
                    RejectionReason.InvalidCode => "Codul de acces nu mai este valid. Copiază un cod nou de pe calculatorul controlat.",
                    RejectionReason.Busy => "Calculatorul controlat are deja o sesiune sau o cerere de acces în curs.",
                    RejectionReason.Denied => "Accesul a fost refuzat pe calculatorul controlat.",
                    RejectionReason.ApprovalExpired => "Nimeni nu a aprobat cererea în 30 de secunde pe calculatorul controlat.",
                    _ => "Calculatorul controlat nu a autorizat conexiunea."
                });
            if (response.Kind != Kind.Accepted) throw new AuthenticationException("Conexiunea nu a fost autorizată.");
            progress?.Invoke("Acces aprobat. Primesc configurația monitoarelor.");
            return ssl;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { ssl.Dispose(); throw new ConnectionFailureException(ConnectionStage.Approval, "Conexiunea TLS funcționează, dar calculatorul controlat nu a confirmat accesul în 60 de secunde.", ex); }
        catch (EndOfStreamException ex)
        { ssl.Dispose(); throw new ConnectionFailureException(ConnectionStage.Approval, "Calculatorul controlat a închis conexiunea înainte de autorizare. Verifică jurnalul lui și folosește un cod nou.", ex); }
        catch { ssl.Dispose(); throw; }
    }

    private static async Task<SslStream> ConnectDirectAsync(Invitation invitation, CancellationToken ct,
        Action<string>? progress)
    {
        var hosts = new[] { invitation.Host }.Concat(invitation.AlternateHosts ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        using var attemptsStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // Race complete pinned TLS handshakes, never just TCP sockets. A stale LAN address
        // may point at another PC. It must not receive the access token or win the race.
        var pending = hosts.Select(h => ConnectEndpointAsync(h, invitation.Port, invitation.Pin,
            attemptsStop.Token, progress)).ToList();
        var failures = new List<ConnectionFailureException>();
        try
        {
            while (pending.Count > 0)
            {
                Task<SslStream> finished = await Task.WhenAny(pending);
                pending.Remove(finished);
                try
                {
                    var stream = await finished;
                    if (ct.IsCancellationRequested) { stream.Dispose(); ct.ThrowIfCancellationRequested(); }
                    return stream;
                }
                catch (ConnectionFailureException ex) { failures.Add(ex); }
            }
            ct.ThrowIfCancellationRequested();
            // If TCP succeeded somewhere, preserve the TLS diagnosis instead of suggesting
            // a firewall change. The other addresses may legitimately be unreachable.
            var tlsFailure = failures.FirstOrDefault(f => f.Stage == ConnectionStage.Tls);
            if (tlsFailure is not null) throw tlsFailure;
            throw new ConnectionFailureException(ConnectionStage.Network,
                $"Nu am putut ajunge la {string.Join(", ", hosts)} pe TCP {invitation.Port}. " +
                "Verifică aceeași rețea LAN sau un VPN comun, apoi «Permite LAN/VPN în firewall» pe calculatorul controlat. " +
                "Între rețele fără rută comună este necesar releul.", failures.FirstOrDefault());
        }
        finally
        {
            await attemptsStop.CancelAsync();
            foreach (var task in pending)
            {
                try { (await task).Dispose(); }
                catch (Exception ex) when (IsConnectionError(ex)) { }
            }
        }
    }

    private static async Task<SslStream> ConnectEndpointAsync(string host, int port, string pin,
        CancellationToken ct, Action<string>? progress)
    {
        progress?.Invoke($"TCP: încerc {host}:{port}.");
        Stream transport;
        try { transport = await DialAsync(host, port, ct); }
        catch (Exception ex) when (IsConnectionError(ex) && !ct.IsCancellationRequested)
        {
            string reason = ex is SocketException socket ? socket.SocketErrorCode.ToString() : "timp de așteptare expirat";
            progress?.Invoke($"TCP {host}:{port}: {reason}.");
            throw new ConnectionFailureException(ConnectionStage.Network, $"TCP {host}:{port}: {reason}.", ex);
        }
        progress?.Invoke($"TCP conectat la {host}:{port}. Verific certificatul TLS.");
        try { return await SecureClientAsync(transport, pin, ct); }
        catch (Exception ex) when (IsConnectionError(ex) && !ct.IsCancellationRequested)
        {
            throw new ConnectionFailureException(ConnectionStage.Tls,
                $"TCP funcționează către {host}:{port}, dar certificatul TLS nu a fost verificat sau negocierea a expirat. " +
                "Folosește codul actual de pe calculatorul controlat și verifică jurnalul lui.", ex);
        }
    }

    private static bool IsConnectionError(Exception ex) => ex is IOException or SocketException
        or AuthenticationException or OperationCanceledException;

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
