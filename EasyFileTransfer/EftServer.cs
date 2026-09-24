using EasyFileTransfer.Internal;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileTransfer
{
    /// <summary>
    /// Receives files sent by <see cref="EftClient"/> and stores them in
    /// <see cref="EftServerOptions.SaveDirectory"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var server = new EftServer(new EftServerOptions { SaveDirectory = @"C:\Inbox", AccessToken = token });
    /// server.FileReceived += (s, e) => Console.WriteLine($"Saved {e.FilePath}");
    /// server.Start();
    /// </code>
    /// </example>
    public class EftServer : IDisposable
    {
        private static readonly TimeSpan BusyTimeout = TimeSpan.FromSeconds(5);

        private readonly object _gate = new object();
        private readonly ConcurrentDictionary<Task, byte> _connections = new ConcurrentDictionary<Task, byte>();
        private TcpListener? _listener;
        private CancellationTokenSource? _stopCts;
        private Task? _acceptLoop;
        private string _saveDirectory = string.Empty;
        private int _activeConnections;

        /// <summary>
        /// Creates a server. Call <see cref="Start"/> to begin accepting files.
        /// </summary>
        public EftServer(EftServerOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            options.Validate();
        }

        /// <summary>The server's settings. Change them only while the server is stopped.</summary>
        public EftServerOptions Options { get; }

        /// <summary>The address and port the server is listening on, or <c>null</c> when stopped.</summary>
        public IPEndPoint? LocalEndPoint { get; private set; }

        /// <summary>Whether the server is accepting connections.</summary>
        public bool IsRunning => _listener != null;

        /// <summary>
        /// Raised on a thread-pool thread after a file has been received, verified and saved.
        /// Exceptions thrown by handlers are ignored.
        /// </summary>
        public event EventHandler<FileReceivedEventArgs>? FileReceived;

        /// <summary>
        /// Raised on a thread-pool thread when a transfer is rejected or fails. Any partially
        /// written data has already been deleted. Exceptions thrown by handlers are ignored.
        /// </summary>
        public event EventHandler<TransferFailedEventArgs>? TransferFailed;

        /// <summary>
        /// Starts listening for connections in the background and returns immediately.
        /// </summary>
        /// <exception cref="InvalidOperationException">The server is already running.</exception>
        /// <exception cref="SocketException">The port could not be opened.</exception>
        public void Start()
        {
            lock (_gate)
            {
                if (_listener != null)
                {
                    throw new InvalidOperationException("The server is already running.");
                }

                Options.Validate();
                _saveDirectory = Path.GetFullPath(Options.SaveDirectory);
                Directory.CreateDirectory(_saveDirectory);

                var listener = new TcpListener(Options.BindAddress, Options.Port);
                if (Options.BindAddress.Equals(IPAddress.IPv6Any))
                {
                    listener.Server.DualMode = true;
                }

                listener.Start();
                var stopCts = new CancellationTokenSource();
                _listener = listener;
                _stopCts = stopCts;
                LocalEndPoint = (IPEndPoint)listener.LocalEndpoint;
                _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, stopCts.Token));
            }
        }

        /// <summary>
        /// Stops accepting connections, aborts transfers in progress and waits for them to finish.
        /// </summary>
        public async Task StopAsync()
        {
            Task? acceptLoop;
            CancellationTokenSource? stopCts;
            lock (_gate)
            {
                if (_listener == null)
                {
                    return;
                }

                stopCts = _stopCts!;
                acceptLoop = _acceptLoop;
                stopCts.Cancel();
                _listener.Stop();
                _listener = null;
                _stopCts = null;
                _acceptLoop = null;
                LocalEndPoint = null;
            }

            if (acceptLoop != null)
            {
                await acceptLoop.ConfigureAwait(false);
            }

            await Task.WhenAll(_connections.Keys).ConfigureAwait(false);
            stopCts.Dispose();
        }

        /// <summary>Stops the server. See <see cref="StopAsync"/>.</summary>
        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken stopToken)
        {
            while (!stopToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception) when (stopToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException)
                {
                    // A client that resets before the accept completes is not a server failure.
                    continue;
                }

                Task connection = Task.Run(() => HandleConnectionAsync(client, stopToken));
                _connections.TryAdd(connection, 0);
                _ = connection.ContinueWith(t => _connections.TryRemove(t, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        private async Task HandleConnectionAsync(TcpClient client, CancellationToken stopToken)
        {
            var transfer = new Transfer();
            bool busy = Interlocked.Increment(ref _activeConnections) > Options.MaxConcurrentConnections;
            var connection = new Connection(client, busy ? BusyTimeout : Options.IdleTimeout, stopToken);
            Exception? failure = null;
            try
            {
                transfer.RemoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                client.NoDelay = true;
                await ReceiveAsync(connection, busy, transfer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = connection.Translate(ex);
            }
            finally
            {
                connection.Dispose();
                Interlocked.Decrement(ref _activeConnections);
                DeleteQuietly(transfer.TempPath);
            }

            // Raised only after cleanup, so handlers never see a leftover temp file or a stale connection count.
            if (failure != null && !stopToken.IsCancellationRequested)
            {
                RaiseTransferFailed(failure, transfer.RemoteEndPoint);
            }
        }

        private async Task ReceiveAsync(Connection connection, bool busy, Transfer transfer)
        {
            Stream stream = connection.TcpClient.GetStream();
            connection.Stream = stream;
            if (Options.ServerCertificate != null)
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                connection.Stream = ssl;
                connection.ResetTimer();
                await ssl.AuthenticateAsServerAsync(Options.ServerCertificate).ConfigureAwait(false);
            }

            // Hello.
            byte[] nonce = Protocol.CreateNonce();
            var hello = new byte[Protocol.HelloLength];
            Protocol.WriteMagic(hello, 0);
            hello[Protocol.MagicLength] = Protocol.Version;
            hello[Protocol.MagicLength + 1] = (byte)(busy ? EftStatus.ServerBusy : EftStatus.Success);
            hello[Protocol.MagicLength + 2] = Options.AccessToken != null ? Protocol.FlagAuthRequired : (byte)0;
            Buffer.BlockCopy(nonce, 0, hello, Protocol.MagicLength + 3, nonce.Length);
            await connection.WriteAsync(hello, 0, hello.Length).ConfigureAwait(false);
            await connection.FlushAsync().ConfigureAwait(false);
            if (busy)
            {
                throw new EftException(EftStatus.ServerBusy);
            }

            // Request header. Every length is checked before anything is allocated from it.
            var header = new byte[Protocol.RequestHeaderLength];
            await connection.ReadExactlyAsync(header, 0, header.Length).ConfigureAwait(false);
            if (!Protocol.HasMagic(header, 0))
            {
                await RejectAsync(connection, EftStatus.ProtocolError).ConfigureAwait(false);
            }

            if (header[Protocol.MagicLength] != Protocol.Version)
            {
                await RejectAsync(connection, EftStatus.UnsupportedVersion).ConfigureAwait(false);
            }

            int nameLength = Protocol.ReadUInt16(header, Protocol.RequestHeaderLength - 2);
            if (nameLength == 0 || nameLength > Protocol.MaxFileNameBytes)
            {
                await RejectAsync(connection, EftStatus.InvalidFileName).ConfigureAwait(false);
            }

            var rest = new byte[nameLength + 8];
            await connection.ReadExactlyAsync(rest, 0, rest.Length).ConfigureAwait(false);

            if (Options.AccessToken != null)
            {
                byte[] expectedProof = Protocol.ComputeProof(Options.AccessToken, nonce);
                if (!Protocol.FixedTimeEquals(header, Protocol.MagicLength + 1, expectedProof, Protocol.ProofLength))
                {
                    await RejectAsync(connection, EftStatus.AuthenticationFailed).ConfigureAwait(false);
                }
            }

            string fileName;
            try
            {
                fileName = Protocol.StrictUtf8.GetString(rest, 0, nameLength);
            }
            catch (ArgumentException)
            {
                fileName = string.Empty;
            }

            if (!FileNameValidator.IsValid(fileName) || !IsInsideSaveDirectory(fileName))
            {
                await RejectAsync(connection, EftStatus.InvalidFileName).ConfigureAwait(false);
            }

            long length = Protocol.ReadInt64(rest, nameLength);
            if (length < 0)
            {
                await RejectAsync(connection, EftStatus.ProtocolError).ConfigureAwait(false);
            }

            if (length > Options.MaxFileSize)
            {
                await RejectAsync(connection, EftStatus.FileTooLarge).ConfigureAwait(false);
            }

            if (length > GetAvailableFreeSpace())
            {
                await RejectAsync(connection, EftStatus.InsufficientStorage).ConfigureAwait(false);
            }

            // Receive into a hidden temp file so a partial or corrupt upload never appears under
            // its real name. FileMode.CreateNew never follows or overwrites an existing entry.
            string tempPath = Path.Combine(_saveDirectory, "." + Guid.NewGuid().ToString("N") + ".eftpart");
            FileStream? tempFile = null;
            try
            {
                tempFile = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, Protocol.BufferSize, useAsync: true);
                transfer.TempPath = tempPath;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await RejectAsync(connection, EftStatus.ServerError, ex).ConfigureAwait(false);
            }

            byte[] actualHash;
            using (var file = tempFile!)
            {
                await WriteStatusAsync(connection, EftStatus.Success).ConfigureAwait(false);

                var buffer = new byte[Protocol.BufferSize];
                long remaining = length;
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    while (remaining > 0)
                    {
                        int read = await connection.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining)).ConfigureAwait(false);
                        if (read == 0)
                        {
                            throw new EndOfStreamException("The client disconnected before sending the whole file.");
                        }

                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer, 0, read, connection.Token).ConfigureAwait(false);
                        remaining -= read;
                    }

                    actualHash = hash.GetHashAndReset();
                }

                await file.FlushAsync(connection.Token).ConfigureAwait(false);
            }

            var expectedHash = new byte[Protocol.HashLength];
            await connection.ReadExactlyAsync(expectedHash, 0, expectedHash.Length).ConfigureAwait(false);
            if (!Protocol.FixedTimeEquals(expectedHash, 0, actualHash, Protocol.HashLength))
            {
                await RejectAsync(connection, EftStatus.IntegrityCheckFailed).ConfigureAwait(false);
            }

            string finalPath;
            try
            {
                finalPath = MoveToUniqueName(tempPath, fileName);
                transfer.TempPath = null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await RejectAsync(connection, EftStatus.ServerError, ex).ConfigureAwait(false);
                throw;
            }

            await WriteStatusAsync(connection, EftStatus.Success).ConfigureAwait(false);
            RaiseFileReceived(new FileReceivedEventArgs(finalPath, fileName, length, transfer.RemoteEndPoint));
        }

        private static async Task WriteStatusAsync(Connection connection, EftStatus status)
        {
            await connection.WriteAsync(new[] { (byte)status }, 0, 1).ConfigureAwait(false);
            await connection.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>Tells the client why its request was refused, then aborts the connection.</summary>
        private static async Task RejectAsync(Connection connection, EftStatus status, Exception? innerException = null)
        {
            try
            {
                await WriteStatusAsync(connection, status).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException)
            {
                // The client is gone; the rejection below is still what gets reported.
            }

            throw innerException is null
                ? new EftException(status)
                : new EftException(status, EftException.DescribeStatus(status), innerException);
        }

        /// <summary>Defense in depth: the validated name must resolve to a direct child of the save directory.</summary>
        private bool IsInsideSaveDirectory(string fileName)
        {
            string fullPath = Path.GetFullPath(Path.Combine(_saveDirectory, fileName));
            string? parent = Path.GetDirectoryName(fullPath);
            return parent != null
                && string.Equals(
                    parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    _saveDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.Ordinal);
        }

        private long GetAvailableFreeSpace()
        {
            try
            {
                return new DriveInfo(_saveDirectory).AvailableFreeSpace;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
            {
                // Network shares and some mounts cannot be queried; let the write itself fail if space runs out.
                return long.MaxValue;
            }
        }

        /// <summary>Moves the temp file to <paramref name="fileName"/>, appending " (n)" instead of overwriting.</summary>
        private string MoveToUniqueName(string tempPath, string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            for (int i = 0; i < 10000; i++)
            {
                string candidate = i == 0 ? fileName : $"{stem} ({i}){extension}";
                string destination = Path.Combine(_saveDirectory, candidate);
                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    continue;
                }

                try
                {
                    File.Move(tempPath, destination);
                    return destination;
                }
                catch (IOException) when (File.Exists(destination) || Directory.Exists(destination))
                {
                    // Another transfer claimed the name first.
                }
            }

            throw new IOException($"Could not find a free file name for '{fileName}'.");
        }

        private static void DeleteQuietly(string? path)
        {
            if (path == null)
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Best effort; the hidden .eftpart file can be removed manually.
            }
        }

        private void RaiseFileReceived(FileReceivedEventArgs args)
        {
            try
            {
                FileReceived?.Invoke(this, args);
            }
#pragma warning disable CA1031 // A faulty event handler must not take the server down.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }

        private void RaiseTransferFailed(Exception exception, IPEndPoint? remoteEndPoint)
        {
            try
            {
                TransferFailed?.Invoke(this, new TransferFailedEventArgs(exception, remoteEndPoint));
            }
#pragma warning disable CA1031 // A faulty event handler must not take the server down.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }

        #region Legacy API (0.1.x)

        /// <summary>
        /// Creates a server that saves files to <paramref name="SaveTo"/> and listens on <paramref name="Port"/>.
        /// </summary>
        [Obsolete("Use new EftServer(new EftServerOptions { SaveDirectory = ..., Port = ... }) instead.")]
        public EftServer(string SaveTo, int Port)
            : this(new EftServerOptions { SaveDirectory = SaveTo, Port = Port })
        {
        }

        /// <summary>Directory received files are saved to.</summary>
        [Obsolete("Use Options.SaveDirectory instead.")]
        public string SaveTo => Options.SaveDirectory;

        /// <summary>Port the server listens on.</summary>
        [Obsolete("Use Options.Port instead.")]
        public int Port => Options.Port;

        /// <summary>
        /// Starts the server and blocks the calling thread until <see cref="StopAsync"/> or
        /// <see cref="Dispose"/> is called.
        /// </summary>
        [Obsolete("Use Start() (non-blocking) and StopAsync() instead.")]
        public void StartServer()
        {
            Start();
            Task? acceptLoop;
            lock (_gate)
            {
                acceptLoop = _acceptLoop;
            }

            acceptLoop?.GetAwaiter().GetResult();
        }

        #endregion

        private sealed class Transfer
        {
            public IPEndPoint? RemoteEndPoint { get; set; }

            /// <summary>Temp file to delete when the transfer does not complete.</summary>
            public string? TempPath { get; set; }
        }
    }
}
