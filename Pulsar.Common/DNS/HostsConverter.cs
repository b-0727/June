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
            List<Host> hostsList = new List<Host>();

            if (string.IsNullOrEmpty(rawHosts)) return hostsList;

            var hosts = rawHosts.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var hostEntry in hosts)
            {
                string trimmedEntry = hostEntry.Trim();
                if (trimmedEntry.Length == 0)
                {
                    continue;
                }

                if (Uri.TryCreate(trimmedEntry, UriKind.Absolute, out Uri uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    ushort port = (ushort)(uri.IsDefaultPort
                        ? (uri.Scheme == Uri.UriSchemeHttps ? 443 : 80)
                        : uri.Port);

                    hostsList.Add(new Host
                    {
                        Hostname = uri.Host,
                        Port = port,
                        RawHost = trimmedEntry
                    });
                    continue;
                }

                if (TryParseHostAndPort(trimmedEntry, out string hostname, out ushort parsedPort))
                {
                    hostsList.Add(new Host
                    {
                        Hostname = hostname,
                        Port = parsedPort,
                        RawHost = trimmedEntry
                    });
                    continue;
                }

                hostsList.Add(new Host
                {
                    Hostname = trimmedEntry,
                    Port = 0,
                    RawHost = trimmedEntry
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

        // NOTE: Avoids newer BCL helpers such as IPEndPoint.TryParse or range syntax so the
        // client libraries continue to build on the legacy .NET Framework target (net472).
        private static bool TryParseHostAndPort(string value, out string hostname, out ushort port)
        {
            hostname = null;
            port = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();

            if (!trimmed.Contains("://"))
            {
                if (Uri.TryCreate("tcp://" + trimmed, UriKind.Absolute, out var parsedUri) &&
                    parsedUri != null &&
                    !string.IsNullOrEmpty(parsedUri.Host) && parsedUri.Port > 0)
                {
                    hostname = parsedUri.Host;
                    port = (ushort)parsedUri.Port;
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
                        string bracketPort = trimmed.Substring(closingBracket + 2).Trim();
                        ushort parsedBracketPort;
                        if (ushort.TryParse(bracketPort, out parsedBracketPort))
                        {
                            hostname = addressPart;
                            port = parsedBracketPort;
                            return true;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(addressPart))
                    {
                        hostname = addressPart;
                        return false;
                    }
                }

                return false;
            }

            int firstColon = trimmed.IndexOf(':');
            int lastColon = trimmed.LastIndexOf(':');

            if (firstColon > -1 && firstColon == lastColon && lastColon < trimmed.Length - 1)
            {
                string hostPart = trimmed.Substring(0, lastColon).Trim();
                string trailingPort = trimmed.Substring(lastColon + 1).Trim();

                ushort parsedPort;
                if (hostPart.Length > 0 && ushort.TryParse(trailingPort, out parsedPort))
                {
                    hostname = hostPart;
                    port = parsedPort;
                    return true;
                }
            }

            return false;
        }
    }
}
