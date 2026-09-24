using System;
using System.IO;

namespace EasyFileTransfer
{
    /// <summary>
    /// Thrown when a transfer is rejected or fails at the protocol level.
    /// Network failures surface as <see cref="IOException"/> or <see cref="TimeoutException"/>.
    /// </summary>
    public class EftException : IOException
    {
        /// <summary>Creates a new <see cref="EftException"/>.</summary>
        public EftException(EftStatus status)
            : this(status, DescribeStatus(status))
        {
        }

        /// <summary>Creates a new <see cref="EftException"/> with a custom message.</summary>
        public EftException(EftStatus status, string message)
            : base(message)
        {
            Status = status;
        }

        /// <summary>Creates a new <see cref="EftException"/> with a custom message and inner exception.</summary>
        public EftException(EftStatus status, string message, Exception innerException)
            : base(message, innerException)
        {
            Status = status;
        }

        /// <summary>The reason the transfer failed.</summary>
        public EftStatus Status { get; }

        internal static string DescribeStatus(EftStatus status)
        {
            switch (status)
            {
                case EftStatus.Success: return "The operation completed successfully.";
                case EftStatus.ServerBusy: return "The server is busy. Try again later.";
                case EftStatus.AuthenticationFailed: return "The server rejected the access token.";
                case EftStatus.InvalidFileName: return "The file name is not allowed.";
                case EftStatus.FileTooLarge: return "The file is larger than the server allows.";
                case EftStatus.InsufficientStorage: return "The server does not have enough free disk space.";
                case EftStatus.IntegrityCheckFailed: return "The file was corrupted in transit (SHA-256 mismatch).";
                case EftStatus.ProtocolError: return "The peer sent an invalid message.";
                case EftStatus.ServerError: return "The server failed to store the file.";
                case EftStatus.UnsupportedVersion: return "The peer uses an unsupported protocol version.";
                default: return "Unknown status " + (byte)status + ".";
            }
        }
    }
}
