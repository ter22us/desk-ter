using System.Collections.Concurrent;
using System.Net.Security;
using System.Threading.Channels;
using Ter22.Core;

namespace Ter22.Windows.Sessions;

internal sealed class ViewerSession : IDisposable
{
    private readonly SslStream _stream;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly ConcurrentDictionary<Guid, string> _views = new();
    private readonly Channel<(Kind Kind, byte[] Data)> _commands = Channel.CreateBounded<(Kind, byte[])>(
        new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private TaskCompletionSource? _frameReady;
    private DisplayLayout _layout;
    private int _stopped;
    private int _disposed;
    public DisplayLayout Layout => Volatile.Read(ref _layout);
    public event Action<DisplayLayout>? LayoutChanged;
    public event Action<FrameHeader, byte[]>? FrameReceived;
    public event Action<string>? Closed;
    public Task Completion { get; private set; } = Task.CompletedTask;

    private ViewerSession(SslStream stream, DisplayLayout layout) { _stream = stream; _layout = layout; }
    public static async Task<ViewerSession> ConnectAsync(Invitation invitation, CancellationToken ct)
    {
        var stream = await Connections.ConnectViewerAsync(invitation, ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var packet = await Wire.ReadAsync(stream, timeout.Token, 16384);
            if (packet.Kind != Kind.Displays) throw new InvalidDataException("Configurația monitoarelor lipsește.");
            var layout = packet.Json<DisplayLayout>(); layout.Validate();
            return new ViewerSession(stream, layout);
        }
        catch { stream.Dispose(); throw; }
    }
    public void Start() => Completion = Task.Run(RunAsync);
    public bool HasView(string id) => _views.Values.Contains(id, StringComparer.Ordinal);
    public void SetView(Guid id, string displayId) => _views[id] = displayId;
    public void RemoveView(Guid id) { _views.TryRemove(id, out _); ReleaseInputs(); if (_views.IsEmpty) Stop(); }
    public void Input(RemoteInput input) => Enqueue(Kind.Input, Wire.Json(input));
    public void ReleaseInputs() => Enqueue(Kind.Release, []);
    private void Enqueue(Kind kind, byte[] bytes)
    {
        if (Volatile.Read(ref _stopped) != 0) return;
        // Never silently drop key-up events; close the connection so the host releases all held input.
        if (!_commands.Writer.TryWrite((kind, bytes))) Stop();
    }
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        _stop.Cancel(); _stream.Dispose();
    }

    private async Task RunAsync()
    {
        string message = "Sesiune închisă.";
        var reader = ReceiveAsync(_stop.Token);
        var writer = CommandsAsync(_stop.Token);
        var frames = RequestFramesAsync(_stop.Token);
        try
        {
            Task first = await Task.WhenAny(reader, writer, frames);
            await first;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!_stop.IsCancellationRequested) message = HostSession.SafeMessage(ex); }
        finally
        {
            Interlocked.Exchange(ref _stopped, 1);
            await _stop.CancelAsync(); _stream.Dispose(); _commands.Writer.TryComplete();
            try { await Task.WhenAll(reader, writer, frames); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { }
            Closed?.Invoke(message);
        }
    }
    private async Task SendAsync(Kind kind, byte[] bytes, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await _writer.WaitAsync(timeout.Token);
        try { await Wire.WriteAsync(_stream, kind, bytes, timeout.Token); }
        finally { _writer.Release(); }
    }
    private async Task CommandsAsync(CancellationToken ct)
    {
        await foreach (var command in _commands.Reader.ReadAllAsync(ct))
            await SendAsync(command.Kind, command.Data, ct);
    }
    private async Task RequestFramesAsync(CancellationToken ct)
    {
        int index = 0;
        while (!ct.IsCancellationRequested)
        {
            var views = _views.ToArray();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _frameReady, ready);
            if (views.Length == 0) await SendAsync(Kind.Ping, [], ct);
            else
            {
                var next = views[index++ % views.Length];
                if (index == int.MaxValue) index = 0;
                await SendAsync(Kind.FrameRequest, Wire.Json(new FrameRequest(next.Key, next.Value)), ct);
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            await Task.Delay(100, ct); // One outstanding frame; bounded latency and no unbounded frame queue.
        }
    }
    private async Task ReceiveAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var packet = await Wire.ReadAsync(_stream, timeout.Token);
            switch (packet.Kind)
            {
                case Kind.Displays:
                    var layout = packet.Json<DisplayLayout>(); layout.Validate();
                    Volatile.Write(ref _layout, layout); LayoutChanged?.Invoke(layout); break;
                case Kind.Frame:
                    var (header, jpeg) = Wire.UnpackFrame(packet.Payload);
                    JpegGuard.Validate(jpeg, header.ImageWidth, header.ImageHeight);
                    if (_views.TryGetValue(header.ViewId, out string? selected) && selected == header.DisplayId)
                        FrameReceived?.Invoke(header, jpeg);
                    Volatile.Read(ref _frameReady)?.TrySetResult(); break;
                case Kind.Pong: Volatile.Read(ref _frameReady)?.TrySetResult(); break;
                default: throw new InvalidDataException("Răspuns neașteptat de la calculator.");
            }
        }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stream.Dispose(); _stop.Dispose(); _writer.Dispose();
    }
}
