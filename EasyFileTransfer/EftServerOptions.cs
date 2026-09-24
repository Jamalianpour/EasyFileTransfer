using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace EasyFileTransfer
{
    /// <summary>
    /// Settings for <see cref="EftServer"/>.
    /// </summary>
    public sealed class EftServerOptions
    {
        /// <summary>
        /// Directory received files are written to. It is created if it does not exist.
        /// Files are never written outside this directory.
        /// </summary>
        public string SaveDirectory { get; set; } = string.Empty;

        /// <summary>TCP port to listen on. Use 0 to let the operating system pick a free port.</summary>
        public int Port { get; set; } = 1300;

        /// <summary>
        /// Local address to listen on. Defaults to <see cref="IPAddress.Any"/> (all IPv4 interfaces);
        /// use <see cref="IPAddress.Loopback"/> to accept only local connections.
        /// </summary>
        public IPAddress BindAddress { get; set; } = IPAddress.Any;

        /// <summary>
        /// Shared secret clients must know to upload files. When <c>null</c> (the default)
        /// anyone who can reach the port can upload. Strongly recommended on any
        /// network you do not fully trust; use a long random value.
        /// </summary>
        public string? AccessToken { get; set; }

        /// <summary>
        /// Certificate (with private key) used to encrypt connections with TLS. When <c>null</c>
        /// (the default) files travel over the network unencrypted.
        /// </summary>
        public X509Certificate? ServerCertificate { get; set; }

        /// <summary>Largest file, in bytes, the server accepts. <c>null</c> means no limit.</summary>
        public long? MaxFileSize { get; set; }

        /// <summary>Maximum number of connections handled at the same time. Defaults to 10.</summary>
        public int MaxConcurrentConnections { get; set; } = 10;

        /// <summary>
        /// Maximum time the server waits on a single network operation before dropping the
        /// connection. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(SaveDirectory))
            {
                throw new ArgumentException("SaveDirectory is required.", nameof(SaveDirectory));
            }

            if (Port < IPEndPoint.MinPort || Port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(Port));
            }

            if (BindAddress is null)
            {
                throw new ArgumentNullException(nameof(BindAddress));
            }

            if (AccessToken is not null && AccessToken.Length == 0)
            {
                throw new ArgumentException("AccessToken must not be empty. Use null to disable authentication.", nameof(AccessToken));
            }

            if (MaxFileSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxFileSize));
            }

            if (MaxConcurrentConnections < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxConcurrentConnections));
            }

            if (IdleTimeout <= TimeSpan.Zero && IdleTimeout != System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(IdleTimeout));
            }

            _ = Path.GetFullPath(SaveDirectory);
        }
    }
}
