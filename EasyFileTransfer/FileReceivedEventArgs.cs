using System;
using System.Net;

namespace EasyFileTransfer
{
    /// <summary>Data for the <see cref="EftServer.FileReceived"/> event.</summary>
    public sealed class FileReceivedEventArgs : EventArgs
    {
        internal FileReceivedEventArgs(string filePath, string requestedFileName, long length, IPEndPoint? remoteEndPoint)
        {
            FilePath = filePath;
            RequestedFileName = requestedFileName;
            Length = length;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>Full path of the saved file. A suffix such as " (1)" is added when a file with the same name already exists.</summary>
        public string FilePath { get; }

        /// <summary>File name sent by the client.</summary>
        public string RequestedFileName { get; }

        /// <summary>Size of the file in bytes.</summary>
        public long Length { get; }

        /// <summary>Address of the client that sent the file.</summary>
        public IPEndPoint? RemoteEndPoint { get; }
    }

    /// <summary>Data for the <see cref="EftServer.TransferFailed"/> event.</summary>
    public sealed class TransferFailedEventArgs : EventArgs
    {
        internal TransferFailedEventArgs(Exception exception, IPEndPoint? remoteEndPoint)
        {
            Exception = exception;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// Why the transfer failed. An <see cref="EftException"/> means the server rejected the
        /// request; other exceptions are network or disk errors.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>Address of the client, when known.</summary>
        public IPEndPoint? RemoteEndPoint { get; }
    }
}
