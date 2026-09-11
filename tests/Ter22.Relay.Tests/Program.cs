using System.Net.Security;
using System.Security.Cryptography;
using Ter22.Core;

// The relay code arrives via a private pipe, never through command-line arguments or logs.
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
try
{
    string code = await Console.In.ReadLineAsync(timeout.Token) ?? throw new InvalidDataException("Codul releului lipseste de la stdin.");
    var relay = RelayAddress.Parse(code);
    using var hostIdentity = Identity.Create();
    string token = Identity.NewSecret(), route = Guid.NewGuid().ToString("N");
    byte[] payload = RandomNumberGenerator.GetBytes(200_000);
    Task host = Task.Run(async () =>
    {
        using var transport = await Connections.RelayAsync(relay, route, true, timeout.Token);
        using var inner = await Connections.SecureServerAsync(transport, hostIdentity, timeout.Token);
        if (!await Connections.AuthenticateViewerAsync(inner, token, timeout.Token))
            throw new InvalidDataException("Token respins in conexiunea intre clienti.");
        await Wire.WriteAsync(inner, Kind.Accepted, ReadOnlyMemory<byte>.Empty, timeout.Token);
        var request = await Wire.ReadAsync(inner, timeout.Token);
        if (request.Kind != Kind.Ping || !request.Payload.SequenceEqual(payload))
            throw new InvalidDataException("Date modificate spre gazda.");
        await Wire.WriteAsync(inner, Kind.Pong, request.Payload, timeout.Token);
    });
    try
    {
        var invitation = new Invitation(Wire.Version, "", 0, hostIdentity.Pin, token, route, relay);
        using var viewer = await ConnectWhenRegistered(invitation, host, timeout.Token);
        await Wire.WriteAsync(viewer, Kind.Ping, payload, timeout.Token);
        var response = await Wire.ReadAsync(viewer, timeout.Token);
        if (response.Kind != Kind.Pong || !response.Payload.SequenceEqual(payload))
            throw new InvalidDataException("Date modificate spre client.");
        await host;
        Console.WriteLine("PASS releu instalat: doi clienti, TLS intre capete si 200000 octeti verificati in ambele sensuri.");
    }
    finally
    {
        await timeout.CancelAsync();
        try { await host; }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Security.Authentication.AuthenticationException) { }
    }
    return 0;
}
catch (Exception ex) when (ex is not OutOfMemoryException)
{
    // Do not print an input value or exception payload that could contain the relay secret.
    Console.Error.WriteLine($"FAIL verificare releu instalat: {ex.GetType().Name}");
    return 1;
}

static async Task<SslStream> ConnectWhenRegistered(Invitation invitation, Task host, CancellationToken ct)
{
    // The production protocol registers a host asynchronously; retry only the rendezvous
    // stage. A failure in end-to-end TLS/authentication is never retried or relaxed.
    while (true)
    {
        if (host.IsFaulted) await host;
        try { return await Connections.ConnectViewerAsync(invitation, ct); }
        catch (ConnectionFailureException ex) when (ex.Stage == ConnectionStage.Relay)
        { await Task.Delay(100, ct); }
    }
}
