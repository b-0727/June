using System;

namespace Pulsar.Server.Networking
{
    public sealed class TailscaleFunnelInfo
    {
        public string Endpoint { get; }
        public string Hostname { get; }
        public ushort Port { get; }
        public string[] TailscaleIps { get; }

        public bool HasEndpoint => !string.IsNullOrWhiteSpace(Endpoint);

        public TailscaleFunnelInfo(string endpoint, string hostname, ushort port, string[] tailscaleIps)
        {
            Endpoint = endpoint ?? string.Empty;
            Hostname = hostname ?? string.Empty;
            Port = port;
            TailscaleIps = tailscaleIps ?? Array.Empty<string>();
        }
    }
}
