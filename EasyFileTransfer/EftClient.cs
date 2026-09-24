using EasyFileTransfer.Internal;
using EasyFileTransfer.Model;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileTransfer
{
    /// <summary>
    /// Sends files to an <see cref="EftServer"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var client = new EftClient("192.168.1.10", 1300, new EftClientOptions { AccessToken = token });
    /// await client.SendFileAsync(@"C:\big.iso", new Progress&lt;EftProgress&gt;(p => Console.WriteLine(p.Percentage)));
    /// </code>
    /// </example>
    public class EftClient
    {
        private readonly EftClientOptions _options;

        /// <summary>
        /// Creates a client for the server at <paramref name="host"/>:<paramref name="port"/>.
        /// </summary>
        /// <param name="host">Host name or IP address of the server.</param>
        /// <param name="port">Port the server listens on.</param>
        /// <param name="options">Optional settings such as the access token and TLS.</param>
        public EftClient(string host, int port, EftClientOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host is required.", nameof(host));
            }

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            _options = options ?? new EftClientOptions();
            _options.Validate();
            Host = host;
            Port = port;
        }

        /// <summary>Host name or IP address of the server.</summary>
        public string Host { get; }

        /// <summary>Port the server listens on.</summary>
        public int Port { get; }

        /// <summary>
        /// Sends the file at <paramref name="filePath"/>. The server stores it under the same file name.
        /// </summary>
        /// <param name="filePath">Path of the file to send.</param>
        /// <param name="progress">Receives progress updates; may be <c>null</c>.</param>
        /// <param name="cancellationToken">Aborts the transfer.</param>
        /// <exception cref="EftException">The server rejected the file or the transfer failed its integrity check.</exception>
        /// <exception cref="IOException">A network or disk error occurred.</exception>
        /// <exception cref="TimeoutException">A network operation took longer than <see cref="EftClientOptions.Timeout"/>.</exception>
        public async Task SendFileAsync(string filePath, IProgress<EftProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, Protocol.BufferSize, useAsync: true))
            {
                await SendAsync(file, Path.GetFileName(filePath), progress, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends the remaining contents of <paramref name="content"/> (from its current position to its end)
        /// as a file called <paramref name="fileName"/>.
        /// </summary>
        /// <param name="content">A readable, seekable stream.</param>
        /// <param name="fileName">Name the server stores the file under. Must not contain path separators.</param>
        /// <param name="progress">Receives progress updates; may be <c>null</c>.</param>
        /// <param name="cancellationToken">Aborts the transfer.</param>
        /// <exception cref="EftException">The server rejected the file or the transfer failed its integrity check.</exception>
        /// <exception cref="IOException">A network or disk error occurred.</exception>
        /// <exception cref="TimeoutException">A network operation took longer than <see cref="EftClientOptions.Timeout"/>.</exception>
        public async Task SendAsync(Stream content, string fileName, IProgress<EftProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!content.CanRead || !content.CanSeek)
            {
                throw new ArgumentException("The stream must be readable and seekable.", nameof(content));
            }

            if (!FileNameValidator.IsValid(fileName))
            {
                throw new EftException(EftStatus.InvalidFileName, $"'{fileName}' is not a valid file name.");
            }

            byte[] nameBytes = Protocol.StrictUtf8.GetBytes(fileName);
            if (nameBytes.Length > Protocol.MaxFileNameBytes)
            {
                throw new EftException(EftStatus.InvalidFileName, "The file name is too long.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            long length = content.Length - content.Position;

            using (var connection = new Connection(new TcpClient(), _options.Timeout, cancellationToken))
            {
                try
                {
                    await TransferAsync(connection, content, length, nameBytes, progress).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is EftException))
                {
                    Exception translated = connection.Translate(ex);
                    if (ReferenceEquals(translated, ex))
                    {
                        throw;
                    }

                    throw translated;
                }
            }
        }

        private async Task TransferAsync(Connection connection, Stream content, long length, byte[] nameBytes, IProgress<EftProgress>? progress)
        {
            await ConnectAsync(connection).ConfigureAwait(false);

            // Server hello.
            var hello = new byte[Protocol.HelloLength];
            await connection.ReadExactlyAsync(hello, 0, hello.Length).ConfigureAwait(false);
            if (!Protocol.HasMagic(hello, 0))
            {
                throw new EftException(EftStatus.ProtocolError, "The remote endpoint is not an EasyFileTransfer server.");
            }

            if (hello[Protocol.MagicLength] != Protocol.Version)
            {
                throw new EftException(EftStatus.UnsupportedVersion, $"The server speaks protocol version {hello[Protocol.MagicLength]}; this client requires version {Protocol.Version}.");
            }

            ThrowIfNotSuccess(hello[Protocol.MagicLength + 1]);

            bool authRequired = (hello[Protocol.MagicLength + 2] & Protocol.FlagAuthRequired) != 0;
            if (authRequired && _options.AccessToken is null)
            {
                throw new EftException(EftStatus.AuthenticationFailed, "The server requires an access token. Set EftClientOptions.AccessToken.");
            }

            var nonce = new byte[Protocol.NonceLength];
            Buffer.BlockCopy(hello, Protocol.MagicLength + 3, nonce, 0, nonce.Length);

            // Request.
            var request = new byte[Protocol.RequestHeaderLength + nameBytes.Length + 8];
            Protocol.WriteMagic(request, 0);
            request[Protocol.MagicLength] = Protocol.Version;
            byte[] proof = Protocol.ComputeProof(_options.AccessToken, nonce);
            Buffer.BlockCopy(proof, 0, request, Protocol.MagicLength + 1, Protocol.ProofLength);
            Protocol.WriteUInt16(request, Protocol.RequestHeaderLength - 2, nameBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, request, Protocol.RequestHeaderLength, nameBytes.Length);
            Protocol.WriteInt64(request, Protocol.RequestHeaderLength + nameBytes.Length, length);
            await connection.WriteAsync(request, 0, request.Length).ConfigureAwait(false);
            await connection.FlushAsync().ConfigureAwait(false);

            await ReadStatusAsync(connection).ConfigureAwait(false);

            // File content followed by its SHA-256.
            var buffer = new byte[Protocol.BufferSize];
            long sent = 0;
            int lastReportedPercent = -1;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                progress?.Report(new EftProgress(0, length));
                while (sent < length)
                {
                    int toRead = (int)Math.Min(buffer.Length, length - sent);
                    int read = await content.ReadAsync(buffer, 0, toRead, connection.Token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("The source stream ended before all bytes were sent.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await connection.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                    sent += read;

                    int percent = (int)(sent * 100 / Math.Max(length, 1));
                    if (progress != null && percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        progress.Report(new EftProgress(sent, length));
                    }
                }

                byte[] digest = hash.GetHashAndReset();
                await connection.WriteAsync(digest, 0, digest.Length).ConfigureAwait(false);
                await connection.FlushAsync().ConfigureAwait(false);
            }

            await ReadStatusAsync(connection).ConfigureAwait(false);
            if (length == 0)
            {
                progress?.Report(new EftProgress(0, 0));
            }
        }

        private async Task ConnectAsync(Connection connection)
        {
            TcpClient tcp = connection.TcpClient;
            connection.ResetTimer();
            await tcp.ConnectAsync(Host, Port).ConfigureAwait(false);
            tcp.NoDelay = true;

            Stream stream = tcp.GetStream();
            connection.Stream = stream;

            if (_options.UseTls)
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false, _options.ServerCertificateValidationCallback);
                connection.Stream = ssl;
                connection.ResetTimer();
                await ssl.AuthenticateAsClientAsync(_options.TlsTargetHost ?? Host).ConfigureAwait(false);
            }
        }

        private static async Task ReadStatusAsync(Connection connection)
        {
            var status = new byte[1];
            await connection.ReadExactlyAsync(status, 0, 1).ConfigureAwait(false);
            ThrowIfNotSuccess(status[0]);
        }

        private static void ThrowIfNotSuccess(byte status)
        {
            if (status != (byte)EftStatus.Success)
            {
                throw new EftException((EftStatus)status);
            }
        }

        #region Legacy API (0.1.x)

        /// <summary>
        /// Percentage of file send progress for the most recent <see cref="Send"/> call.
        /// </summary>
        [Obsolete("Shared static state is not thread-safe. Pass an IProgress<EftProgress> to SendFileAsync instead.")]
        public static int ProgressValue;

        /// <summary>
        /// Sends a file and blocks until the transfer completes.
        /// </summary>
        /// <param name="FilePath">file location that you want to send</param>
        /// <param name="TargetIP">IP Address of target system</param>
        /// <param name="Port">Server listen to this port</param>
        /// <returns>An object with status and description</returns>
        [Obsolete("Use new EftClient(host, port, options).SendFileAsync(path) instead.")]
        public static Response Send(string FilePath, string TargetIP, int Port)
        {
            try
            {
                var progress = new SynchronousProgress(p => ProgressValue = (int)Math.Ceiling(p.Percentage));
                new EftClient(TargetIP, Port).SendFileAsync(FilePath, progress).GetAwaiter().GetResult();
                return new Response { status = 1, description = "send successfully." };
            }
            catch (Exception e)
            {
                return new Response { status = -1, description = "Error: " + e.Message };
            }
        }

        private sealed class SynchronousProgress : IProgress<EftProgress>
        {
            private readonly Action<EftProgress> _handler;

            public SynchronousProgress(Action<EftProgress> handler) => _handler = handler;

            public void Report(EftProgress value) => _handler(value);
        }

        #endregion
    }
}
