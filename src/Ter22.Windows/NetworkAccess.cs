using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace Ter22.Windows;

internal sealed record LocalNetworkAddress(string Address, string Adapter, bool HasGateway)
{
    public override string ToString() => $"{Address} — {Adapter}";

    public static LocalNetworkAddress[] Read()
    {
        var result = new List<LocalNetworkAddress>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up
                || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var properties = adapter.GetIPProperties();
                bool gateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                    && !g.Address.Equals(IPAddress.Any));
                foreach (var entry in properties.UnicastAddresses)
                {
                    var ip = entry.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)
                        || ip.Equals(IPAddress.Any) || ip.GetAddressBytes()[0] >= 224) continue;
                    result.Add(new(ip.ToString(), adapter.Name, gateway));
                }
            }
            catch (NetworkInformationException) { /* An adapter can disappear during enumeration. */ }
        }
        return result.OrderByDescending(a => a.HasGateway).ThenBy(a => a.Adapter, StringComparer.Ordinal)
            .ThenBy(a => a.Address, StringComparer.Ordinal).DistinctBy(a => a.Address).ToArray();
    }
}

internal static class FirewallAccess
{
    // These scopes cover private LANs and common VPN ranges, including CGNAT VPNs.
    // No public Internet source addresses, UDP, other application or other port are allowed.
    internal const string RemoteScope = "LocalSubnet,10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,100.64.0.0/10";
    internal static string Executable => Environment.ProcessPath ?? throw new IOException("Calea executabilului lipsește.");
    internal static string RuleName(int port) => "Desk Ter LAN-VPN " +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(Executable).ToUpperInvariant())))[..16]
        + " TCP " + port.ToString(CultureInfo.InvariantCulture);

    // Called only by the separate, explicitly elevated helper. The remote session never runs elevated.
    internal static string? Configure(int port)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("Configurarea Windows Firewall necesită aprobarea administratorului.");
        dynamic policy = CreateCom("HNetCfg.FwPolicy2");
        dynamic rule = CreateCom("HNetCfg.FWRule");
        object? rulesObject = null;
        try
        {
            dynamic rules = policy.Rules;
            rulesObject = rules;
            rule.Name = RuleName(port);
            rule.Description = "Desk Ter: acces TCP autentificat prin cod si certificat, din LAN/VPN. Creat la cererea utilizatorului.";
            rule.ApplicationName = Path.GetFullPath(Executable);
            rule.Protocol = 6;
            rule.LocalPorts = port.ToString(CultureInfo.InvariantCulture);
            rule.RemoteAddresses = RemoteScope;
            rule.Direction = 1;
            rule.Profiles = 1 | 2 | 4; // VPN adapters may be classified as Public by Windows.
            rule.InterfaceTypes = "All";
            rule.Action = 1;
            rule.EdgeTraversal = false;
            rule.Enabled = true;
            // Add replaces this application's rule with the same deterministic name.
            // Existing user/admin block rules are never removed or disabled.
            rules.Add(rule);
            if ((int)policy.LocalPolicyModifyState != 0)
                return "Regula a fost salvată, dar politica Windows nu permite aplicarea regulilor locale. Administratorul rețelei trebuie să permită aplicația.";
            int profiles = (int)policy.CurrentProfileTypes;
            foreach (dynamic existing in rules)
            {
                try
                {
                    if ((bool)existing.Enabled && (int)existing.Direction == 1 && (int)existing.Action == 0
                        && ((int)existing.Protocol is 6 or 256) && ((int)existing.Profiles & profiles) != 0
                        && string.Equals((string?)existing.ApplicationName, Executable, StringComparison.OrdinalIgnoreCase))
                        return "Regula LAN/VPN a fost salvată, dar există și o regulă de blocare pentru acest executabil. " +
                            "În Windows Defender Firewall → Setări complexe → Reguli de intrare, verifică regula de blocare Ter22 Remote. " +
                            "O regulă de blocare are prioritate; aplicația nu o elimină automat.";
                }
                finally { Marshal.ReleaseComObject((object)existing); }
            }
            return null;
        }
        finally
        {
            if (rulesObject is not null) Marshal.ReleaseComObject(rulesObject);
            Marshal.ReleaseComObject((object)rule);
            Marshal.ReleaseComObject((object)policy);
        }
    }

    private static object CreateCom(string name) => Activator.CreateInstance(Type.GetTypeFromProgID(name, throwOnError: true)!)
        ?? throw new IOException("Windows Firewall nu este disponibil.");
}
