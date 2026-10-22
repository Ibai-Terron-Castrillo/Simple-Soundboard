using System.Net;
using System.Net.NetworkInformation;
using LocalSoundboard.Services;
using Xunit;

namespace LocalSoundboard.Tests;

public sealed class RemoteNetworkDiscoveryTests
{
    [Fact]
    public void RouterConnectedWifiComesBeforeHostOnlyAndVirtualAdapters()
    {
        var urls = RemoteNetworkDiscovery.OrderUrls(
        [
            Address("172.23.48.1", false),
            Address("192.168.56.1", false),
            Address("192.168.1.33", true, NetworkInterfaceType.Wireless80211)
        ], 5050);

        Assert.Equal("http://192.168.1.33:5050", urls[0]);
        Assert.Equal(3, urls.Count);
    }

    [Fact]
    public void RouterConnectedEthernetComesBeforeVpn()
    {
        var urls = RemoteNetworkDiscovery.OrderUrls(
        [
            Address("10.0.0.2", true, NetworkInterfaceType.Ppp),
            Address("192.168.1.10", true)
        ], 6000);

        Assert.Equal("http://192.168.1.10:6000", urls[0]);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("169.254.10.20")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public void NonLanAddressesAreNotOfferedToTheTablet(string address)
        => Assert.Empty(RemoteNetworkDiscovery.OrderUrls([Address(address, true)], 5050));

    [Fact]
    public void LoopbackAndTunnelAdaptersAreNotOffered()
        => Assert.Empty(RemoteNetworkDiscovery.OrderUrls(
        [
            Address("10.0.0.2", true, NetworkInterfaceType.Tunnel),
            Address("10.0.0.3", true, NetworkInterfaceType.Loopback)
        ], 5050));

    [Fact]
    public void AddressesAreDeduplicatedAndOfflineDoesNotInventALanUrl()
    {
        Assert.Single(RemoteNetworkDiscovery.OrderUrls([Address("192.168.1.10", false), Address("192.168.1.10", true)], 5050));
        Assert.Empty(RemoteNetworkDiscovery.OrderUrls([], 5050));
    }

    private static RemoteNetworkDiscovery.Address Address(string ip, bool gateway,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet) => new(IPAddress.Parse(ip), gateway, type);
}
