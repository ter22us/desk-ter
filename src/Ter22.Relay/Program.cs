using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Ter22.Core;

if (!OperatingSystem.IsLinux())
{
    Console.Error.WriteLine("Releul se instalează pe Linux; aplicația de control se instalează pe Windows.");
    return 2;
}
if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out int port)
    || (args.Length == 3 && args[2] != "--show-code"))
{
    Console.Error.WriteLine("Utilizare: Ter22.Relay nume-dns-sau-ip-public port [--show-code]");
    return 2;
}
Codes.ValidateHost(args[0], port);
string statePath = Path.GetFullPath("relay-state.json");
RelayState state;
if (File.Exists(statePath))
{
    if (new FileInfo(statePath).Length > 65536) throw new InvalidDataException("Fișier identitate prea mare.");
    var mode = File.GetUnixFileMode(statePath);
    if ((mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0)
        throw new InvalidDataException("Protejează relay-state.json cu chmod 600 înainte de pornire.");
    state = JsonSerializer.Deserialize<RelayState>(File.ReadAllBytes(statePath)) ?? throw new InvalidDataException("Identitate invalidă.");
}
else
{
    using var generated = Identity.Create();
    state = new RelayState(Convert.ToBase64String(generated.Certificate.Export(X509ContentType.Pfx)), Identity.NewSecret());
    using var file = new FileStream(statePath, new FileStreamOptions
    {
        Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
        UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
    });
    JsonSerializer.Serialize(file, state);
    file.Flush(true);
}
if (!Identity.ValidSecret(state.Key)) throw new InvalidDataException("Cheie releu invalidă.");
using var identity = new Identity(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(state.Pfx), null,
    X509KeyStorageFlags.EphemeralKeySet));
if (identity.Certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow.AddDays(1))
    throw new InvalidDataException("Certificatul releului expiră. Generează o identitate nouă și reconfigurează calculatoarele.");
var address = new RelayAddress(args[0], port, identity.Pin, state.Key);
if (args.Length == 3)
{
    Console.WriteLine(address.ToCode());
    return 0;
}
Console.WriteLine($"Releu privat pornit pe portul {port}. Oprire: Ctrl+C.");
Console.WriteLine("Pentru codul de configurare folosește --show-code într-un terminal privat.");
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
var broker = new RelayBroker(identity, state.Key, new IPEndPoint(IPAddress.Any, port));
await broker.RunAsync(shutdown.Token);
return 0;

internal sealed record RelayState(string Pfx, string Key);
