using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace optimizerDuck.Common.Helpers;

public static class HttpClientFactory
{
    /// <summary>
    ///     Creates an <see cref="HttpClient"/> with explicit TLS 1.2+ enforcement and
    ///     a <see cref="RemoteCertificateValidationCallback"/> that validates server
    ///     certificates via the system certificate store while logging any failures.
    /// </summary>
    /// <param name="timeout">Optional per-request timeout.</param>
    /// <param name="logger">Optional logger for TLS diagnostic output.</param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    public static HttpClient CreateClient(TimeSpan? timeout = null, ILogger? logger = null)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
                {
                    if (errors == SslPolicyErrors.None)
                        return true;

                    var certInfo =
                        certificate != null
                            ? $"Subject: {certificate.Subject}, Issuer: {certificate.Issuer}, "
                                + $"Expires: {certificate.GetExpirationDateString()}"
                            : "No certificate presented";

                    var remoteEndpoint = GetRemoteEndpoint(sender);

                    if (logger != null)
                    {
                        logger.LogWarning(
                            "TLS certificate validation failed for {Endpoint}. {CertInfo}. Errors: {Errors}",
                            remoteEndpoint,
                            certInfo,
                            errors
                        );
                    }
                    else
                    {
                        Trace.TraceWarning(
                            "TLS certificate validation failed for {0}. {1}. Errors: {2}",
                            remoteEndpoint,
                            certInfo,
                            errors
                        );
                    }

                    return false;
                },
            },
        };

        var client = new HttpClient(handler, disposeHandler: true);

        if (timeout.HasValue)
            client.Timeout = timeout.Value;

        return client;
    }

    private static string GetRemoteEndpoint(object? sender)
    {
        if (sender is not HttpRequestMessage request)
            return "Unknown";

        var uri = request.RequestUri;
        if (uri == null)
            return "Unknown";

        return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
    }
}
