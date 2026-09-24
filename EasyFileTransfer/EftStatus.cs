namespace EasyFileTransfer
{
    /// <summary>
    /// Status codes exchanged between <see cref="EftClient"/> and <see cref="EftServer"/>.
    /// </summary>
    public enum EftStatus : byte
    {
        /// <summary>The operation completed successfully.</summary>
        Success = 0,

        /// <summary>The server has reached its concurrent connection limit.</summary>
        ServerBusy = 1,

        /// <summary>The access token was missing or did not match the server's token.</summary>
        AuthenticationFailed = 2,

        /// <summary>The file name is empty, too long, or contains characters that are not allowed.</summary>
        InvalidFileName = 3,

        /// <summary>The file exceeds the server's <see cref="EftServerOptions.MaxFileSize"/>.</summary>
        FileTooLarge = 4,

        /// <summary>The server does not have enough free disk space for the file.</summary>
        InsufficientStorage = 5,

        /// <summary>The SHA-256 hash of the received data did not match the hash sent by the client.</summary>
        IntegrityCheckFailed = 6,

        /// <summary>The peer sent data that does not follow the EasyFileTransfer protocol.</summary>
        ProtocolError = 7,

        /// <summary>The server failed to store the file.</summary>
        ServerError = 8,

        /// <summary>The peer speaks a protocol version this library does not support.</summary>
        UnsupportedVersion = 9,
    }
}
