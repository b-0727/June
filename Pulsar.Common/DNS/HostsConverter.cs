using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Pulsar.Common.DNS
{
    public class HostsConverter
    {
        public List<Host> RawHostsToList(string rawHosts, bool server = false)
        {
            var hostsList = new List<Host>();

            if (string.IsNullOrWhiteSpace(rawHosts))
            {
                return hostsList;
            }

            var entries = rawHosts.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var trimmed = entry.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                Uri parsedUri;
                if (Uri.TryCreate(trimmed, UriKind.Absolute, out parsedUri) &&
                    (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps))
                {
                    ushort httpPort = (ushort)(parsedUri.IsDefaultPort
                        ? (parsedUri.Scheme == Uri.UriSchemeHttps ? 443 : 80)
                        : parsedUri.Port);

                    hostsList.Add(new Host
                    {
                        Hostname = parsedUri.Host,
                        Port = httpPort,
                        RawHost = trimmed
                    });
                    continue;
                }

                string parsedHost;
                ushort parsedPort;
                if (TryParseHostAndPort(trimmed, out parsedHost, out parsedPort))
                {
                    hostsList.Add(new Host
                    {
                        Hostname = parsedHost,
                        Port = parsedPort,
                        RawHost = trimmed
                    });
                    continue;
                }

                hostsList.Add(new Host
                {
                    Hostname = trimmed,
                    Port = 0,
                    RawHost = trimmed
                });
            }

            return hostsList;
        }

        public string ListToRawHosts(IList<Host> hosts)
        {
            if (hosts == null || hosts.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                if (host == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(';');
                }

                builder.Append(host);
            }

            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            return builder.ToString();
        }

        private static bool TryParseHostAndPort(string value, out string hostname, out ushort port)
        {
            hostname = null;
            port = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();

            // Handle bracketed IPv6 addresses: [address]:port
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                int closingBracket = trimmed.IndexOf(']');
                if (closingBracket > 0)
                {
                    string addressPart = trimmed.Substring(1, closingBracket - 1).Trim();
                    if (addressPart.Length == 0)
                    {
                        return false;
                    }

                    if (closingBracket + 1 < trimmed.Length && trimmed[closingBracket + 1] == ':')
                    {
                        string portSegment = trimmed.Substring(closingBracket + 2).Trim();
                        ushort parsed;
                        if (ushort.TryParse(portSegment, out parsed))
                        {
                            hostname = addressPart;
                            port = parsed;
                            return true;
                        }
                        hostname = addressPart;
                        return false;
                    }

                    hostname = addressPart;
                }

                return false;
            }

            // Give URI parsing a chance (covers "host:port" without needing new APIs)
            if (!trimmed.Contains("://"))
            {
                Uri tcpUri;
                if (Uri.TryCreate("tcp://" + trimmed, UriKind.Absolute, out tcpUri) &&
                    tcpUri != null &&
                    tcpUri.Port > 0 &&
                    !string.IsNullOrEmpty(tcpUri.Host))
                {
                    hostname = tcpUri.Host;
                    port = (ushort)tcpUri.Port;
                    return true;
                }
            }

            // Fall back to a simple split on the last colon for IPv4 or hostname entries.
            int firstColon = trimmed.IndexOf(':');
            if (firstColon == -1)
            {
                hostname = trimmed;
                return false;
            }

            int lastColon = trimmed.LastIndexOf(':');
            if (firstColon == lastColon && lastColon < trimmed.Length - 1)
            {
                string hostSegment = trimmed.Substring(0, lastColon).Trim();
                string portSegment = trimmed.Substring(lastColon + 1).Trim();
                ushort parsedPort;
                if (hostSegment.Length > 0 && ushort.TryParse(portSegment, out parsedPort))
                {
                    hostname = hostSegment;
                    port = parsedPort;
                    return true;
                }

                hostname = hostSegment;
                return false;
            }

            hostname = trimmed;
            return false;
        }

        private static bool TryParseHostAndPort(string value, out string hostname, out ushort port)
        {
            hostname = null;
            port = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();

            if (!trimmed.Contains("://") && Uri.TryCreate("tcp://" + trimmed, UriKind.Absolute, out var uri))
            if (!value.Contains("://") && Uri.TryCreate($"tcp://{value}", UriKind.Absolute, out var uri))
            {
                if (uri.Port > 0)
                {
                    hostname = uri.Host;
                    port = (ushort)uri.Port;
                    return true;
                }
            }

            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                int closingBracket = trimmed.IndexOf(']');
                if (closingBracket > 0)
                {
                    string addressPart = trimmed.Substring(1, closingBracket - 1).Trim();

                    if (closingBracket + 1 < trimmed.Length && trimmed[closingBracket + 1] == ':')
                    {
                        string portPart = trimmed.Substring(closingBracket + 2).Trim();
                        if (ushort.TryParse(portPart, out var parsedPort))
                        {
                            hostname = addressPart;
                            port = parsedPort;
                            return true;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(addressPart))
                    {
                        hostname = addressPart;
                        return false;
                    }
                }
            }
            else
            {
                int firstColon = trimmed.IndexOf(':');
                int lastColon = trimmed.LastIndexOf(':');

                if (firstColon > -1 && firstColon == lastColon && lastColon < trimmed.Length - 1)
                {
                    string hostPart = trimmed.Substring(0, lastColon).Trim();
                    string portPart = trimmed.Substring(lastColon + 1).Trim();

                    if (ushort.TryParse(portPart, out var parsedPort))
                    {
                        hostname = hostPart;
                        port = parsedPort;
                        return true;
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
