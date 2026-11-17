using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TKS.LeadPilot.Core.Services;

public class SsrfValidationResult
{
    public bool IsSafe { get; set; }
    public Uri? CleanUri { get; set; }
    public IPAddress? ResolvedIp { get; set; }
    public string? Reason { get; set; }
}

public interface ISsrfValidator
{
    Task<SsrfValidationResult> ValidateUrlAsync(string? url, CancellationToken ct = default);
}

public class SsrfValidator : ISsrfValidator
{
    public async Task<SsrfValidationResult> ValidateUrlAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new SsrfValidationResult { IsSafe = false, Reason = "URL is empty or null." };
        }

        var candidate = url.Trim();
        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return new SsrfValidationResult { IsSafe = false, Reason = "Malformed URL format." };
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new SsrfValidationResult { IsSafe = false, Reason = $"Prohibited URI scheme '{uri.Scheme}'. Only HTTP and HTTPS are permitted." };
        }

        var host = uri.DnsSafeHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            return new SsrfValidationResult { IsSafe = false, Reason = "Invalid or empty hostname." };
        }

        // Direct hostname checks
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return new SsrfValidationResult { IsSafe = false, Reason = "Local or internal hostnames are prohibited." };
        }

        // DNS Resolution check to prevent DNS rebinding to private/loopback IPs
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            if (addresses.Length == 0)
            {
                return new SsrfValidationResult { IsSafe = false, Reason = $"DNS resolution failed for '{host}'." };
            }

            foreach (var ip in addresses)
            {
                if (IsPrivateOrRestrictedIp(ip))
                {
                    return new SsrfValidationResult
                    {
                        IsSafe = false,
                        ResolvedIp = ip,
                        Reason = $"Hostname '{host}' resolved to restricted IP address '{ip}'."
                    };
                }
            }

            return new SsrfValidationResult
            {
                IsSafe = true,
                CleanUri = uri,
                ResolvedIp = addresses[0]
            };
        }
        catch (SocketException ex)
        {
            return new SsrfValidationResult { IsSafe = false, Reason = $"DNS lookup error: {ex.Message}" };
        }
    }

    public static bool IsPrivateOrRestrictedIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
            // IPv4-mapped IPv6 address check
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // 0.0.0.0/8 (Current network)
            if (bytes[0] == 0) return true;

            // 10.0.0.0/8 (Private Class A)
            if (bytes[0] == 10) return true;

            // 127.0.0.0/8 (Loopback)
            if (bytes[0] == 127) return true;

            // 169.254.0.0/16 (Link-Local, AWS/GCP/Azure Metadata: 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // 172.16.0.0/12 (Private Class B: 172.16.0.0 - 172.31.255.255)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.0.0/16 (Private Class C)
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 224.0.0.0/4 (Multicast)
            if (bytes[0] >= 224 && bytes[0] <= 239) return true;

            // 240.0.0.0/4 (Reserved)
            if (bytes[0] >= 240) return true;

            // 255.255.255.255 (Broadcast)
            if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) return true;
        }

        return false;
    }
}
