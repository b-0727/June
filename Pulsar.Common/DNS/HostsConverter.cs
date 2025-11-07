using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Pulsar.Common.DNS
{
    public class HostsConverter
    {

        public List<Host> RawHostsToList(string rawHosts, bool server = false)
        {
            List<Host> hostsList = new List<Host>();

            if (string.IsNullOrEmpty(rawHosts)) return hostsList;

            var hosts = rawHosts.Split(';');

            foreach (var hostEntry in hosts)
            {
                if (string.IsNullOrWhiteSpace(hostEntry)) continue;

                if (Uri.TryCreate(hostEntry, UriKind.Absolute, out Uri uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    ushort port = (ushort)(uri.IsDefaultPort
                        ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 80)
                        : uri.Port);

                    hostsList.Add(new Host
                    {
                        Hostname = uri.Host,
                        Port = port,
                        RawHost = hostEntry
                    });
                    continue;
                }

                if (TryParseHostAndPort(hostEntry, out string hostname, out ushort parsedPort))
                {
                    hostsList.Add(new Host
                    {
                        Hostname = hostname,
                        Port = parsedPort,
                        RawHost = hostEntry
                    });
                    continue;
                }

                hostsList.Add(new Host
                {
                    Hostname = hostEntry,
                    Port = 0,
                    RawHost = hostEntry
                });
            }

            return hostsList;
        }

        public string ListToRawHosts(IList<Host> hosts)
        {
            StringBuilder rawHosts = new StringBuilder();

            foreach (var host in hosts)
            {
                rawHosts.Append(host + ";");
            }

            return rawHosts.ToString();
        }

        private static bool TryParseHostAndPort(string value, out string hostname, out ushort port)
        {
            hostname = null;
            port = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.Contains("://") && Uri.TryCreate($"tcp://{value}", UriKind.Absolute, out var uri))
            {
                if (uri.Port > 0)
                {
                    hostname = uri.Host;
                    port = (ushort)uri.Port;
                    return true;
                }
            }

            if (IPEndPoint.TryParse(value, out var endpoint))
            {
                hostname = endpoint.Address.ToString();
                port = (ushort)endpoint.Port;
                return true;
            }

            var lastColon = value.LastIndexOf(':');
            if (lastColon > -1 && lastColon < value.Length - 1)
            {
                var portPart = value[(lastColon + 1)..];
                if (ushort.TryParse(portPart, out var parsed))
                {
                    hostname = value.Substring(0, lastColon).Trim('[', ']');
                    port = parsed;
                    return true;
                }
            }

            return false;
        }
    }
}
