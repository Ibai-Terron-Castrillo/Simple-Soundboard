using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LocalSoundboard.Services;

internal static class RemoteNetworkDiscovery
{
    internal readonly record struct Address(IPAddress Ip, bool HasGateway, NetworkInterfaceType InterfaceType);

    public static IReadOnlyList<string> GetUrls(int port)
    {
        var addresses = new List<Address>();
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up
                        || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                        continue;

                    var properties = adapter.GetIPProperties();
                    var hasGateway = properties.GatewayAddresses.Any(gateway => IsUsableIpv4(gateway.Address));
                    addresses.AddRange(properties.UnicastAddresses.Select(address =>
                        new Address(address.Address, hasGateway, adapter.NetworkInterfaceType)));
                }
                catch (NetworkInformationException)
                {
                    // An adapter can disappear while Windows is changing networks.
                }
            }
        }
        catch (NetworkInformationException)
        {
        }

        return OrderUrls(addresses, port);
    }

    internal static IReadOnlyList<string> OrderUrls(IEnumerable<Address> addresses, int port)
        => addresses.Where(address => IsUsableIpv4(address.Ip)
                && address.InterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .OrderByDescending(address => address.HasGateway)
            .ThenByDescending(address => address.InterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
            .ThenBy(address => address.Ip.ToString(), StringComparer.Ordinal)
            .Select(address => $"http://{address.Ip}:{port}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsUsableIpv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) return false;
        var bytes = address.GetAddressBytes();
        return bytes[0] is > 0 and < 224 && !(bytes[0] == 169 && bytes[1] == 254);
    }
}
