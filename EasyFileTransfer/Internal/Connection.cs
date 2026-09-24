using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileTransfer.Internal
{
    /// <summary>
    /// A TCP connection whose every operation is bounded by an idle timeout and a cancellation
    /// token. When either fires the socket is closed, which unblocks pending I/O on all
    /// target frameworks (older frameworks ignore the token once a read has started).
    /// </summary>
    internal sealed class Connection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly TimeSpan _timeout;
        private readonly CancellationToken _outerToken;
        private readonly CancellationTokenSource _cts;
        private readonly CancellationTokenRegistration _registration;

        public Connection(TcpClient client, TimeSpan timeout, CancellationToken cancellationToken)
        {
            _client = client;
            _timeout = timeout;
            _outerToken = cancellationToken;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _registration = _cts.Token.Register(state => ((TcpClient)state!).Dispose(), client);
        }

        public TcpClient TcpClient => _client;

        public Stream? Stream { get; set; }

        public CancellationToken Token => _cts.Token;

        public bool TimedOut => _cts.IsCancellationRequested && !_outerToken.IsCancellationRequested;

        /// <summary>Starts (or restarts) the idle timer before the next operation.</summary>
        public void ResetTimer() => _cts.CancelAfter(_timeout);

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            ResetTimer();
            return await Stream!.ReadAsync(buffer, offset, count, _cts.Token).ConfigureAwait(false);
        }

        public async Task ReadExactlyAsync(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = await ReadAsync(buffer, offset, count).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("The remote party closed the connection.");
                }

                offset += read;
                count -= read;
            }
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            ResetTimer();
            await Stream!.WriteAsync(buffer, offset, count, _cts.Token).ConfigureAwait(false);
        }

        public async Task FlushAsync()
        {
            ResetTimer();
            await Stream!.FlushAsync(_cts.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Converts the exception an aborted operation produced into the one callers expect:
        /// <see cref="OperationCanceledException"/> for cancellation, <see cref="TimeoutException"/>
        /// for the idle timer, and the original exception otherwise.
        /// </summary>
        public Exception Translate(Exception exception)
        {
            if (exception is EftException)
            {
                return exception;
            }

            if (_outerToken.IsCancellationRequested)
            {
                return new OperationCanceledException("The transfer was canceled.", exception, _outerToken);
            }

            if (TimedOut)
            {
                return new TimeoutException("The network operation timed out.", exception);
            }

            return exception;
        }

        public void Dispose()
        {
            _registration.Dispose();
            _cts.Dispose();
            Stream?.Dispose();
            _client.Dispose();
        }
    }
}
