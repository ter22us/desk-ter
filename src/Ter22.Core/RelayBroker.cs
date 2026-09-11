using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Ter22.Core;

// Authenticated TLS rendezvous. Forwarded bytes contain a second TLS session that
// the broker cannot decrypt: screen pixels, input and host access tokens stay private.
public sealed class RelayBroker(Identity identity, string key, IPEndPoint endpoint)
{
    private sealed class Slot
    {
        public TaskCompletionSource<Stream> Viewer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly TcpListener _listener = new(endpoint);
    private readonly ConcurrentDictionary<string, Slot> _waiting = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _hostCapacity = new(16, 16);
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int RegisteredHostCount => _waiting.Count;
    public Action<string>? Report { get; set; }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start(32);
        using var capacity = new SemaphoreSlim(32);
        var active = new List<Task>();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                if (!await capacity.WaitAsync(0, ct)) { client.Dispose(); continue; }
                active.RemoveAll(t => t.IsCompleted);
                active.Add(HandleLimitedAsync(client, capacity, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            _listener.Stop();
            await Task.WhenAll(active);
        }
    }

    private async Task HandleLimitedAsync(TcpClient client, SemaphoreSlim capacity, CancellationToken ct)
    {
        using (client)
        {
            try { client.NoDelay = true; await HandleAsync(client, ct); }
            catch (Exception ex) when (ex is IOException or System.Security.Authentication.AuthenticationException
                or OperationCanceledException or InvalidDataException or System.Text.Json.JsonException or SocketException or ObjectDisposedException)
            { Report?.Invoke("Conexiune închisă sau cerere respinsă."); }
            finally { capacity.Release(); }
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken ct)
    {
        using var ssl = await Connections.SecureServerAsync(client.GetStream(), identity, ct);
        RelayHello hello;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var packet = await Wire.ReadAsync(ssl, timeout.Token, 8192);
            if (packet.Kind != Kind.RelayHello) return;
            hello = packet.Json<RelayHello>();
        }
        if (hello.Version != Wire.Version || !Identity.EqualSecret(hello.Key, key)
            || !Guid.TryParseExact(hello.Route, "N", out _)) return;

        if (!hello.Host)
        {
            if (!_waiting.TryGetValue(hello.Route, out var existing) || !existing.Viewer.TrySetResult(ssl))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                await Wire.WriteAsync(ssl, Kind.Error, ReadOnlyMemory<byte>.Empty, timeout.Token);
                return;
            }
            await existing.Done.Task.WaitAsync(ct);
            return;
        }

        var slot = new Slot();
        if (!await _hostCapacity.WaitAsync(0, ct)) return;
        if (!_waiting.TryAdd(hello.Route, slot)) { _hostCapacity.Release(); return; }
        try
        {
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(ct);
            waiting.CancelAfter(TimeSpan.FromSeconds(90));
            var viewer = await slot.Viewer.Task.WaitAsync(waiting.Token);
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using (var readyTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                readyTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                await Wire.WriteAsync(ssl, Kind.RelayReady, ReadOnlyMemory<byte>.Empty, readyTimeout.Token);
                await Wire.WriteAsync(viewer, Kind.RelayReady, ReadOnlyMemory<byte>.Empty, readyTimeout.Token);
            }
            Task forward = PumpAsync(ssl, viewer, session.Token), reverse = PumpAsync(viewer, ssl, session.Token);
            await Task.WhenAny(forward, reverse);
            await session.CancelAsync();
            ssl.Dispose(); viewer.Dispose();
            try { await Task.WhenAll(forward, reverse); }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        }
        finally { _waiting.TryRemove(hello.Route, out _); slot.Done.TrySetResult(); _hostCapacity.Release(); }
    }

    private static async Task PumpAsync(Stream source, Stream target, CancellationToken ct)
    {
        byte[] buffer = new byte[64 * 1024];
        while (!ct.IsCancellationRequested)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(75));
            int count = await source.ReadAsync(buffer, timeout.Token);
            if (count == 0) return;
            await target.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
            await target.FlushAsync(timeout.Token);
        }
    }
}
