using Pulsar.Server.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace Pulsar.Server.Networking
{
    public sealed class TailscaleFunnelService : IDisposable
    {
        private static readonly Lazy<TailscaleFunnelService> _instance = new(() => new TailscaleFunnelService());

        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(45);
        private readonly object _syncRoot = new();
        private Timer _timer;
        private bool _started;
        private bool _disposed;
        private TailscaleFunnelInfo _current;

        public static TailscaleFunnelService Instance => _instance.Value;

        public event EventHandler<TailscaleFunnelInfo> FunnelEndpointChanged;

        public TailscaleFunnelInfo Current
        {
            get
            {
                lock (_syncRoot)
                {
                    return _current;
                }
            }
        }

        private TailscaleFunnelService()
        {
        }

        public void Start()
        {
            lock (_syncRoot)
            {
                if (_started)
                {
                    return;
                }

                _timer = new Timer(Poll, null, TimeSpan.Zero, _pollInterval);
                _started = true;
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                _timer?.Dispose();
                _timer = null;
                _started = false;
            }
        }

        public void RequestImmediateRefresh()
        {
            Poll(null);
        }

        private void Poll(object state)
        {
            if (_disposed)
            {
                return;
            }

            TailscaleFunnelInfo nextInfo = null;

            try
            {
                nextInfo = FetchFunnelInfo();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tailscale funnel discovery failed: {ex.Message}");
            }

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                if (!HasChanged(_current, nextInfo))
                {
                    return;
                }

                _current = nextInfo;
                PersistInfo(nextInfo);
            }

            if (nextInfo != null)
            {
                try
                {
                    FunnelEndpointChanged?.Invoke(this, nextInfo);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Tailscale funnel change notification failed: {ex.Message}");
                }
            }
        }

        private static bool HasChanged(TailscaleFunnelInfo previous, TailscaleFunnelInfo current)
        {
            if (previous == null && current == null)
            {
                return false;
            }

            if (previous == null || current == null)
            {
                return true;
            }

            if (!string.Equals(previous.Endpoint, current.Endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (previous.Port != current.Port)
            {
                return true;
            }

            if (!string.Equals(previous.Hostname, current.Hostname, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (previous.TailscaleIps.Length != current.TailscaleIps.Length)
            {
                return true;
            }

            for (int i = 0; i < previous.TailscaleIps.Length; i++)
            {
                if (!string.Equals(previous.TailscaleIps[i], current.TailscaleIps[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PersistInfo(TailscaleFunnelInfo info)
        {
            if (info == null || !info.HasEndpoint)
            {
                if (!string.IsNullOrEmpty(Settings.TailscaleFunnelEndpoint))
                {
                    Settings.TailscaleFunnelEndpoint = string.Empty;
                }
                return;
            }

            if (!string.Equals(Settings.TailscaleFunnelEndpoint, info.Endpoint, StringComparison.OrdinalIgnoreCase))
            {
                Settings.TailscaleFunnelEndpoint = info.Endpoint;
            }

            var suggestions = BuildSanSuggestions(info);
            if (suggestions.Length > 0)
            {
                var existing = Settings.TailscaleCertificateSans ?? Array.Empty<string>();
                var merged = existing
                    .Concat(suggestions)
                    .Where(entry => !string.IsNullOrWhiteSpace(entry))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (merged.Length != existing.Length || !existing.SequenceEqual(merged, StringComparer.OrdinalIgnoreCase))
                {
                    Settings.TailscaleCertificateSans = merged;
                }
            }
        }

        private static string[] BuildSanSuggestions(TailscaleFunnelInfo info)
        {
            var values = new List<string>();

            if (!string.IsNullOrWhiteSpace(info.Hostname))
            {
                values.Add(info.Hostname);
            }

            if (info.TailscaleIps != null)
            {
                foreach (var ip in info.TailscaleIps)
                {
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        values.Add(ip);
                    }
                }
            }

            return values.ToArray();
        }

        private static TailscaleFunnelInfo FetchFunnelInfo()
        {
            var statusJson = RunCommand("status --json");
            var (dnsName, ips) = ParseStatus(statusJson);

            var funnelOutput = RunCommand("funnel status");
            var endpoint = ParseFunnelEndpoint(funnelOutput, dnsName);

            if (string.IsNullOrWhiteSpace(endpoint.Endpoint))
            {
                return null;
            }

            return new TailscaleFunnelInfo(endpoint.Endpoint, endpoint.Hostname, endpoint.Port, ips);
        }

        private static (string Endpoint, string Hostname, ushort Port) ParseFunnelEndpoint(string output, string fallbackHost)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return BuildEndpointFromHost(fallbackHost);
            }

            var match = Regex.Match(output, @"https?://(?<host>[\w\.-]+)(:(?<port>\d+))?");
            if (match.Success)
            {
                var host = match.Groups["host"].Value;
                ushort port = 443;

                if (match.Groups["port"].Success && ushort.TryParse(match.Groups["port"].Value, out var parsedPort))
                {
                    port = parsedPort;
                }

                return ($"https://{host}:{port}", host, port);
            }

            return BuildEndpointFromHost(fallbackHost);
        }

        private static (string Endpoint, string Hostname, ushort Port) BuildEndpointFromHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return (string.Empty, string.Empty, 0);
            }

            host = host.Trim('.');
            ushort port = Settings.ListenPort;
            var endpoint = $"{host}:{port}";

            if (!host.Contains("://"))
            {
                endpoint = $"https://{host}:{port}";
            }

            return (endpoint, host, port);
        }

        private static (string Hostname, string[] Ips) ParseStatus(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return (string.Empty, Array.Empty<string>());
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("Self", out var self))
                {
                    return (string.Empty, Array.Empty<string>());
                }

                string dnsName = string.Empty;
                if (self.TryGetProperty("DNSName", out var dnsProperty))
                {
                    dnsName = dnsProperty.GetString() ?? string.Empty;
                }

                dnsName = dnsName?.Trim('.');

                var ips = new List<string>();
                if (self.TryGetProperty("TailscaleIPs", out var ipArray))
                {
                    foreach (var ip in ipArray.EnumerateArray())
                    {
                        var value = ip.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            ips.Add(value);
                        }
                    }
                }

                return (dnsName, ips.ToArray());
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to parse tailscale status JSON: {ex.Message}");
                return (string.Empty, Array.Empty<string>());
            }
        }

        private static string RunCommand(string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "tailscale",
                        Arguments = arguments,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(3000);

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"tailscale {arguments} exited with {process.ExitCode}: {error}");
                    return string.Empty;
                }

                return string.IsNullOrWhiteSpace(output) ? error : output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to run tailscale {arguments}: {ex.Message}");
                return string.Empty;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
