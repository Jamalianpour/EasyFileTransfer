using System;
using System.Net.Security;

namespace EasyFileTransfer
{
    /// <summary>
    /// Settings for <see cref="EftClient"/>.
    /// </summary>
    public sealed class EftClientOptions
    {
        /// <summary>
        /// Shared secret expected by the server (<see cref="EftServerOptions.AccessToken"/>).
        /// The token itself is never sent; the client proves it knows the token with an
        /// HMAC-SHA256 over a random challenge chosen by the server.
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// Encrypt the connection with TLS. The server must be configured with
        /// <see cref="EftServerOptions.ServerCertificate"/>.
        /// </summary>
        public bool UseTls { get; set; }

        /// <summary>
        /// Host name the server certificate must match. Defaults to the host passed to
        /// the <see cref="EftClient"/> constructor.
        /// </summary>
        public string? TlsTargetHost { get; set; }

        /// <summary>
        /// Custom server certificate validation, for example to pin a self-signed
        /// certificate. When <c>null</c>, the operating system's standard validation is used.
        /// </summary>
        public RemoteCertificateValidationCallback? ServerCertificateValidationCallback { get; set; }

        /// <summary>
        /// Maximum time a single network operation (connect, handshake, read or write)
        /// may take before the transfer is aborted. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        internal void Validate()
        {
            if (Timeout <= TimeSpan.Zero && Timeout != System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be positive or Timeout.InfiniteTimeSpan.");
            }
        }
    }
}
