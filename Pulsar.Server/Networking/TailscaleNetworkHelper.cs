using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Pulsar.Server.Networking
{
    internal static class TailscaleNetworkHelper
    {
        public static IPAddress GetPreferredAddress(bool preferIPv6)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .Where(nic => nic.Name.IndexOf("tailscale", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  nic.Description.IndexOf("tailscale", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                foreach (var networkInterface in interfaces)
                {
                    var properties = networkInterface.GetIPProperties();

                    if (preferIPv6)
                    {
                        var ipv6 = properties.UnicastAddresses
                            .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6);

                        if (ipv6 != null)
                        {
                            return ipv6.Address;
                        }
                    }

                    var ipv4 = properties.UnicastAddresses
                        .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address));

                    if (ipv4 != null)
                    {
                        return ipv4.Address;
                    }

                    if (!preferIPv6)
                    {
                        var ipv6 = properties.UnicastAddresses
                            .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6);

                        if (ipv6 != null)
                        {
                            return ipv6.Address;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
