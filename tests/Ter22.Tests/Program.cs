using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using Ter22.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Protocol: citire fragmentată", FragmentedPacket),
    ("Protocol: limite înainte de alocare", PacketBounds),
    ("Coduri: validare și versiune", CodeValidation),
    ("Coduri: adrese alternative limitate și compatibilitate TRC1", AlternateCodeValidation),
    ("Monitoare: coordonate negative și scalare", MonitorCoordinates),
    ("JPEG: verificarea dimensiunilor înainte de decodare", JpegDimensions),
    ("TLS: certificat fixat și autentificare corectă", () => AuthenticationTest(false, false)),
    ("TLS: respingerea altui certificat", () => AuthenticationTest(true, false)),
    ("TLS: respingerea altui token", () => AuthenticationTest(false, true)),
    ("Conectare: adresă alternativă și respingerea unui alt calculator", AlternateAddressConnection),
    ("Conectare: eroare TCP distinctă de aprobare", NetworkFailure),
    ("Conectare: anulare în negocierea TLS", CancelTls),
    ("TLS 1.2: conectare compatibilă cu Windows 10", Tls12Connection),
    ("Releu: transfer TLS între capete și curățarea sesiunii", RelayRoundTrip),
    ("Releu: respingerea cheii greșite", RelayWrongKey)
};
int failures = 0;
foreach (var test in tests)
{
    try { await test.Run().WaitAsync(TimeSpan.FromSeconds(25)); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.GetType().Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} teste trecute.");
return failures == 0 ? 0 : 1;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static async Task Reject<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception("A fost acceptată o intrare care trebuia respinsă: " + typeof(T).Name);
}
static async Task FragmentedPacket()
{
    using var bytes = new MemoryStream();
    var value = new Authentication(1, Identity.NewSecret());
    await Wire.WriteAsync(bytes, Kind.Auth, Wire.Json(value), default);
    bytes.Position = 0;
    using var fragmented = new FragmentStream(bytes);
    var read = await Wire.ReadAsync(fragmented, default);
    Check(read.Kind == Kind.Auth && read.Json<Authentication>() == value, "Mesaj modificat la fragmentare.");
}
static async Task PacketBounds()
{
    foreach (int length in new[] { -1, 0, Wire.MaxPacket + 1, int.MaxValue })
    {
        byte[] data = new byte[5]; BinaryPrimitives.WriteInt32BigEndian(data, length); data[4] = (byte)Kind.Auth;
        using var stream = new MemoryStream(data);
        await Reject<InvalidDataException>(async () => { await Wire.ReadAsync(stream, default); });
    }
    using var truncated = new MemoryStream(new byte[] { 0, 0, 0, 4, (byte)Kind.Auth, 1 });
    await Reject<EndOfStreamException>(async () => { await Wire.ReadAsync(truncated, default); });
}
static Task CodeValidation()
{
    using var identity = Identity.Create();
    var invitation = new Invitation(1, "127.0.0.1", 45990, identity.Pin, Identity.NewSecret(), Guid.NewGuid().ToString("N"), null);
    Check(Invitation.Parse(invitation.ToCode()) == invitation, "Codul nu revine la aceeași valoare.");
    Check(!Identity.EqualSecret(Identity.NewSecret(), invitation.Token), "Token diferit acceptat.");
    Check(!Identity.EqualSecret("invalid", invitation.Token), "Token invalid acceptat.");
    try { Invitation.Parse((invitation with { Version = 999 }).ToCode()); throw new Exception("Versiune viitoare acceptată."); }
    catch (InvalidDataException) { }
    try { Invitation.Parse((invitation with { Host = "https://example.com/path" }).ToCode()); throw new Exception("Adresă invalidă acceptată."); }
    catch (InvalidDataException) { }
    return Task.CompletedTask;
}
static Task MonitorCoordinates()
{
    var all = new DisplayInfo("*", "All", -1920, -200, 4480, 1640);
    Check(Geometry.Normalize(-1920, -200, all) == (0, 0), "Origine negativă incorectă.");
    Check(Geometry.Normalize(2559, 1439, all) == (65535, 65535), "Ultimul pixel este incorect.");
    var frame = new FrameHeader(Guid.NewGuid(), "left", "v1", -1920, 0, 1920, 1080, 1920, 1080);
    Check(!Geometry.TryMap(400, 0, 800, 800, frame, out _, out _), "Banda neagră acceptă clicuri.");
    Check(Geometry.TryMap(400, 400, 800, 800, frame, out int x, out int y) && x == -960 && y == 540,
        "Centrul imaginii nu corespunde centrului monitorului.");
    Check(!Geometry.TryMap(double.NaN, 10, 800, 800, frame, out _, out _), "NaN acceptat.");
    return Task.CompletedTask;
}
static Task AlternateCodeValidation()
{
    using var identity = Identity.Create();
    var invitation = new Invitation(1, "192.168.1.10", 45990, identity.Pin, Identity.NewSecret(), Guid.NewGuid().ToString("N"), null)
        { AlternateHosts = ["10.77.22.1", "100.64.1.2"] };
    Check(Invitation.Parse(invitation.ToCode()).AlternateHosts!.SequenceEqual(invitation.AlternateHosts), "Adresele alternative s-au pierdut.");
    foreach (var addresses in new[] { new[] { "https://invalid.test" }, new[] { "0.0.0.0" }, new[] { "224.0.0.1" }, Enumerable.Repeat("10.0.0.1", 8).ToArray() })
    {
        try { Invitation.Parse((invitation with { AlternateHosts = addresses }).ToCode()); throw new Exception("Adrese invalide acceptate."); }
        catch (InvalidDataException) { }
    }
    return Task.CompletedTask;
}
static async Task AlternateAddressConnection()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var correct = Identity.Create(); using var wrong = Identity.Create();
    // Same port on two addresses: the first address is reachable but is another computer.
    var first = new TcpListener(IPAddress.Parse("127.0.0.2"), 0); first.Start();
    int port = ((IPEndPoint)first.LocalEndpoint).Port;
    var second = new TcpListener(IPAddress.Loopback, port); second.Start();
    string token = Identity.NewSecret();
    bool leakedToken = false;
    Task wrongHost = Task.Run(async () =>
    {
        try
        {
            using var client = await first.AcceptTcpClientAsync(timeout.Token);
            using var ssl = await Connections.SecureServerAsync(client.GetStream(), wrong, timeout.Token);
            var packet = await Wire.ReadAsync(ssl, timeout.Token);
            leakedToken = packet.Kind == Kind.Auth;
        }
        catch (Exception ex) when (ex is IOException or AuthenticationException or OperationCanceledException) { }
    });
    Task correctHost = Task.Run(async () =>
    {
        using var client = await second.AcceptTcpClientAsync(timeout.Token);
        using var ssl = await Connections.SecureServerAsync(client.GetStream(), correct, timeout.Token);
        Check(await Connections.AuthenticateViewerAsync(ssl, token, timeout.Token), "Tokenul nu a ajuns la adresa corectă.");
        await Wire.WriteAsync(ssl, Kind.Accepted, ReadOnlyMemory<byte>.Empty, timeout.Token);
    });
    try
    {
        var invitation = new Invitation(1, "127.0.0.2", port, correct.Pin, token, Guid.NewGuid().ToString("N"), null)
            { AlternateHosts = ["127.0.0.1"] };
        using var connected = await Connections.ConnectViewerAsync(invitation, timeout.Token);
        await Task.WhenAll(wrongHost, correctHost);
        Check(!leakedToken, "Token trimis calculatorului cu alt certificat.");
    }
    finally { await timeout.CancelAsync(); first.Stop(); second.Stop(); }
}
static async Task NetworkFailure()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    using var identity = Identity.Create();
    // Reserve the endpoint without listening: no other test can take it between allocation and dialing.
    using var reserved = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    reserved.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    int port = ((IPEndPoint)reserved.LocalEndPoint!).Port;
    var invitation = new Invitation(1, "127.0.0.1", port, identity.Pin, Identity.NewSecret(), Guid.NewGuid().ToString("N"), null);
    var messages = new List<string>();
    try { using var stream = await Connections.ConnectViewerAsync(invitation, timeout.Token, messages.Add); throw new Exception("Port fără listener acceptat."); }
    catch (ConnectionFailureException ex) { Check(ex.Stage == ConnectionStage.Network, "Eroarea de rețea a fost prezentată ca eroare de aprobare."); }
    Check(!messages.Any(m => m.StartsWith("TLS verificat", StringComparison.Ordinal)), "TLS raportat înainte de conectare.");
    Check(!messages.Any(m => m.Contains(invitation.Token, StringComparison.Ordinal)), "Token în diagnostic.");
}
static async Task CancelTls()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var request = new CancellationTokenSource();
    using var identity = Identity.Create();
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    try
    {
        var invitation = new Invitation(1, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            identity.Pin, Identity.NewSecret(), Guid.NewGuid().ToString("N"), null);
        Task<SslStream> connecting = Connections.ConnectViewerAsync(invitation, request.Token);
        using var accepted = await listener.AcceptTcpClientAsync(timeout.Token);
        await request.CancelAsync();
        await Reject<OperationCanceledException>(async () => { using var stream = await connecting; });
    }
    finally { listener.Stop(); }
}
static async Task Tls12Connection()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var identity = Identity.Create();
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    string token = Identity.NewSecret();
    var server = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync(timeout.Token);
        using var ssl = new SslStream(client.GetStream(), false);
        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        { ServerCertificate = identity.Certificate, EnabledSslProtocols = SslProtocols.Tls12 }, timeout.Token);
        Check(await Connections.AuthenticateViewerAsync(ssl, token, timeout.Token), "TLS 1.2 nu a autentificat codul.");
        await Wire.WriteAsync(ssl, Kind.Accepted, ReadOnlyMemory<byte>.Empty, timeout.Token);
    });
    try
    {
        var invitation = new Invitation(1, "127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
            identity.Pin, token, Guid.NewGuid().ToString("N"), null);
        using var ssl = await Connections.ConnectViewerAsync(invitation, timeout.Token);
        Check(ssl.SslProtocol == SslProtocols.Tls12, "TLS 1.2 nu a fost negociat.");
        await server;
    }
    finally { listener.Stop(); }
}
static Task JpegDimensions()
{
    byte[] header = [0xff, 0xd8, 0xff, 0xc0, 0, 8, 8, 0, 100, 0, 200, 3];
    JpegGuard.Validate(header, 200, 100);
    try { JpegGuard.Validate(header, 201, 100); throw new Exception("JPEG cu dimensiuni false acceptat."); }
    catch (InvalidDataException) { }
    byte[] damaged = [0xff, 0xd8, 0xff, 0xe0, 0xff, 0xff, 1];
    try { JpegGuard.Validate(damaged, 200, 100); throw new Exception("Segment JPEG trunchiat acceptat."); }
    catch (InvalidDataException) { }
    return Task.CompletedTask;
}
static async Task AuthenticationTest(bool wrongPin, bool wrongToken)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var identity = Identity.Create();
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    string token = Identity.NewSecret();
    var server = Task.Run(async () =>
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var ssl = await Connections.SecureServerAsync(client.GetStream(), identity, timeout.Token);
            bool accepted = await Connections.AuthenticateViewerAsync(ssl, token, timeout.Token);
            if (accepted) await Wire.WriteAsync(ssl, Kind.Accepted, ReadOnlyMemory<byte>.Empty, timeout.Token);
            return accepted;
        }
        catch (Exception ex) when (ex is IOException or AuthenticationException) { return false; }
    });
    try
    {
        var invitation = new Invitation(1, "127.0.0.1", port, wrongPin ? new string('0', 64) : identity.Pin,
            wrongToken ? Identity.NewSecret() : token, Guid.NewGuid().ToString("N"), null);
        bool connected;
        try { using var ssl = await Connections.ConnectViewerAsync(invitation, timeout.Token); connected = true; }
        catch (Exception ex) when (ex is IOException or AuthenticationException) { connected = false; }
        bool expected = !wrongPin && !wrongToken;
        Check(connected == expected && await server == expected, "Rezultat de autentificare incorect.");
    }
    finally { listener.Stop(); }
}
static async Task RelayRoundTrip()
{
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    using var relayIdentity = Identity.Create(); using var hostIdentity = Identity.Create();
    string relayKey = Identity.NewSecret(), accessToken = Identity.NewSecret(), route = Guid.NewGuid().ToString("N");
    var broker = new RelayBroker(relayIdentity, relayKey, new IPEndPoint(IPAddress.Loopback, 0));
    Task service = broker.RunAsync(stop.Token);
    var address = new RelayAddress("127.0.0.1", broker.Port, relayIdentity.Pin, relayKey);
    byte[] payload = System.Security.Cryptography.RandomNumberGenerator.GetBytes(200_000);
    var host = Task.Run(async () =>
    {
        using var transport = await Connections.RelayAsync(address, route, true, stop.Token);
        using var inner = await Connections.SecureServerAsync(transport, hostIdentity, stop.Token);
        Check(await Connections.AuthenticateViewerAsync(inner, accessToken, stop.Token), "Token respins în releu.");
        await Wire.WriteAsync(inner, Kind.Accepted, ReadOnlyMemory<byte>.Empty, stop.Token);
        var packet = await Wire.ReadAsync(inner, stop.Token);
        Check(packet.Payload.SequenceEqual(payload), "Date deteriorate spre host.");
        await Wire.WriteAsync(inner, Kind.Frame, packet.Payload, stop.Token);
    });
    try
    {
        await WaitFor(() => broker.RegisteredHostCount == 1, stop.Token);
        var invitation = new Invitation(1, "", 0, hostIdentity.Pin, accessToken, route, address);
        using var viewer = await Connections.ConnectViewerAsync(invitation, stop.Token);
        await Wire.WriteAsync(viewer, Kind.Frame, payload, stop.Token);
        var response = await Wire.ReadAsync(viewer, stop.Token);
        Check(response.Payload.SequenceEqual(payload), "Date deteriorate spre viewer.");
        viewer.Dispose(); await host;
        await WaitFor(() => broker.RegisteredHostCount == 0, stop.Token);
    }
    finally { await stop.CancelAsync(); await service; }
}
static async Task RelayWrongKey()
{
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var identity = Identity.Create();
    var broker = new RelayBroker(identity, Identity.NewSecret(), new IPEndPoint(IPAddress.Loopback, 0));
    var service = broker.RunAsync(stop.Token);
    try
    {
        var address = new RelayAddress("127.0.0.1", broker.Port, identity.Pin, Identity.NewSecret());
        await Reject<IOException>(async () =>
        { using var stream = await Connections.RelayAsync(address, Guid.NewGuid().ToString("N"), true, stop.Token); });
        Check(broker.RegisteredHostCount == 0, "Host neautorizat înregistrat.");
    }
    finally { await stop.CancelAsync(); await service; }
}
static async Task WaitFor(Func<bool> predicate, CancellationToken ct)
{
    while (!predicate()) await Task.Delay(20, ct);
}
internal sealed class FragmentStream(Stream source) : Stream
{
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => source.ReadAsync(buffer[..Math.Min(3, buffer.Length)], ct);
    public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, Math.Min(3, count));
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
