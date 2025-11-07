using System.Net;

namespace Pulsar.Common.DNS
{
    public class Host
    {
        /// <summary>
        /// Stores the hostname or URL of the Host.
        /// </summary>
        /// <remarks>
        /// Can be an IPv4, IPv6 address, hostname, or HTTPS/HTTP URL when a
        /// Tailscale funnel endpoint is used.
        /// </remarks>
        public string Hostname { get; set; }

        /// <summary>
        /// Stores the original representation of the host as provided by the
        /// operator. This allows the builder UI to keep schemes such as
        /// https:// intact while still providing a normalized Hostname for DNS
        /// resolution.
        /// </summary>
        public string RawHost { get; set; }

        /// <summary>
        /// Stores the IP address of host.
        /// </summary>
        /// <remarks>
        /// Can be an IPv4 or IPv6 address.
        /// </remarks>
        public IPAddress IpAddress { get; set; }

        /// <summary>
        /// Stores the port of the Host.
        /// </summary>
        public ushort Port { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(RawHost))
            {
                if (RawHost.Contains(":") || RawHost.Contains("//"))
                {
                    return RawHost;
                }

                if (Port > 0)
                {
                    return $"{RawHost}:{Port}";
                }
            }

            if (!string.IsNullOrEmpty(Hostname))
            {
                return Port > 0 ? $"{Hostname}:{Port}" : Hostname;
            }

            return string.Empty;
        }
    }
}
