using System.Net;
using System.Net.Sockets;
using Ter22.Core;
using Ter22.Windows.Desktop;

namespace Ter22.Windows.Sessions;

internal sealed class HostSession : IDisposable
{
    private readonly IScreenCapture _capture;
    private readonly Identity _identity = Identity.Create();
    private readonly string _token = Identity.NewSecret();
    private readonly string _route = Guid.NewGuid().ToString("N");
    private readonly SemaphoreSlim _session = new(1, 1);
    private readonly TaskCompletionSource _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Listening => _listening.Task;
    public Action<string>? Status { get; init; }
    public Func<CancellationToken, Task<bool>>? Approve { get; init; }
    public HostSession(IScreenCapture capture) => _capture = capture;
    public Invitation Invitation(string host, int port, RelayAddress? relay) => new(Wire.Version, host, port, _identity.Pin, _token, _route, relay);

    public async Task RunAsync(int port, RelayAddress? relay, CancellationToken ct)
    {
        if (relay is not null)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Status?.Invoke("Așteaptă conexiunea prin releu.");
                    using var transport = await Connections.RelayAsync(relay, _route, true, ct);
                    await HandleAsync(transport, ct);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && !ct.IsCancellationRequested)
                { Status?.Invoke("Releu: reconectare în 3 secunde. " + SafeMessage(ex)); }
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
            return;
        }

        var listener = new TcpListener(IPAddress.Any, port);
        using var capacity = new SemaphoreSlim(4);
        var tasks = new List<Task>();
        listener.Start(4);
        Status?.Invoke($"Acces pornit pe portul {port}. Așteaptă conexiunea.");
        _listening.TrySetResult();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                if (!await capacity.WaitAsync(0, ct)) { client.Dispose(); continue; }
                tasks.RemoveAll(t => t.IsCompleted);
                tasks.Add(HandleClientAsync(client, capacity, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { listener.Stop(); await Task.WhenAll(tasks); }
    }

    private async Task HandleClientAsync(TcpClient client, SemaphoreSlim capacity, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                Status?.Invoke($"Cerere TCP primită de la {client.Client.RemoteEndPoint}. Verific TLS și codul de acces.");
                await HandleAsync(client.GetStream(), ct);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { if (!ct.IsCancellationRequested) Status?.Invoke(SafeMessage(ex)); }
            finally { capacity.Release(); }
        }
    }

    private async Task HandleAsync(Stream transport, CancellationToken ct)
    {
        using var ssl = await Connections.SecureServerAsync(transport, _identity, ct);
        if (!await Connections.AuthenticateViewerAsync(ssl, _token, ct))
        {
            await RejectAsync(ssl, RejectionReason.InvalidCode, ct);
            Status?.Invoke("Cod de acces respins. Copiază codul actual pe calculatorul de pe care te conectezi.");
            return;
        }
        if (!await _session.WaitAsync(0, ct))
        { await RejectAsync(ssl, RejectionReason.Busy, ct); return; }
        var input = new NativeInput();
        try
        {
            if (Approve is not null)
            {
                using var approval = CancellationTokenSource.CreateLinkedTokenSource(ct);
                approval.CancelAfter(TimeSpan.FromSeconds(30));
                Status?.Invoke("Cod verificat. Așteaptă aprobarea în fereastra «cerere de acces» (30 secunde).");
                bool allowed;
                try { allowed = await Approve(approval.Token).WaitAsync(approval.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await RejectAsync(ssl, RejectionReason.ApprovalExpired, ct);
                    Status?.Invoke("Cererea a expirat fără aprobare.");
                    return;
                }
                if (!allowed)
                {
                    await RejectAsync(ssl, approval.IsCancellationRequested ? RejectionReason.ApprovalExpired : RejectionReason.Denied, ct);
                    Status?.Invoke("Cererea de acces nu a fost aprobată.");
                    return;
                }
            }
            await SendAsync(ssl, Kind.Accepted, [], ct);
            var layout = _capture.GetLayout();
            await SendAsync(ssl, Kind.Displays, Wire.Json(layout), ct);
            Status?.Invoke("SESIUNE ACTIVĂ — calculatorul este controlat de la distanță.");
            long windowStart = Environment.TickCount64;
            int messages = 0;
            while (!ct.IsCancellationRequested)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                var packet = await Wire.ReadAsync(ssl, timeout.Token, 8192);
                if (Environment.TickCount64 - windowStart >= 1000) { windowStart = Environment.TickCount64; messages = 0; }
                if (++messages > 500) throw new InvalidDataException("Prea multe comenzi.");
                switch (packet.Kind)
                {
                    case Kind.FrameRequest:
                        var current = _capture.GetLayout();
                        if (current.Revision != layout.Revision)
                        {
                            input.ReleaseAll(); layout = current;
                            await SendAsync(ssl, Kind.Displays, Wire.Json(layout), ct);
                        }
                        var request = packet.Json<FrameRequest>();
                        if (request.ViewId == Guid.Empty) throw new InvalidDataException("Fereastră invalidă.");
                        if (!layout.Displays.Any(d => d.Id == request.DisplayId))
                        {
                            await SendAsync(ssl, Kind.Displays, Wire.Json(layout), ct);
                            await SendAsync(ssl, Kind.Pong, [], ct);
                            break;
                        }
                        byte[] frame = _capture.Capture(request, layout);
                        await SendAsync(ssl, Kind.Frame, frame, ct);
                        break;
                    case Kind.Input:
                        // Re-enumeration prevents stale coordinates from reaching a new monitor arrangement.
                        var inputLayout = _capture.GetLayout();
                        if (inputLayout.Revision != layout.Revision) { input.ReleaseAll(); break; }
                        input.Apply(packet.Json<RemoteInput>(), layout); break;
                    case Kind.Release: input.ReleaseAll(); break;
                    case Kind.Ping: await SendAsync(ssl, Kind.Pong, [], ct); break;
                    default: throw new InvalidDataException("Comandă neașteptată.");
                }
            }
        }
        finally { input.ReleaseAll(); _session.Release(); Status?.Invoke("Sesiune închisă. Accesul rămâne pornit până apeși Oprește accesul."); }
    }

    private static Task RejectAsync(Stream stream, RejectionReason reason, CancellationToken ct) =>
        SendAsync(stream, Kind.Error, Wire.Json(new ConnectionRejection(reason)), ct);

    private static async Task SendAsync(Stream stream, Kind kind, byte[] bytes, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await Wire.WriteAsync(stream, kind, bytes, timeout.Token);
    }
    internal static string SafeMessage(Exception ex) => ex switch
    {
        ConnectionFailureException => ex.Message,
        OperationCanceledException => "Conexiunea a expirat sau a fost oprită.",
        System.Security.Authentication.AuthenticationException => "Certificatul sau autorizarea nu corespund codului de conectare.",
        SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } => "Portul este deja folosit. Alege alt port și generează un cod nou.",
        SocketException socket => $"Conexiune indisponibilă ({socket.SocketErrorCode}). Verifică adresa, portul și paravanul de protecție.",
        _ => ex.Message.Length <= 250 ? ex.Message : "Conexiunea a fost întreruptă."
    };
    public void Dispose() { _identity.Dispose(); _session.Dispose(); }
}
