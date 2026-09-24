namespace EasyFileTransfer
{
    /// <summary>
    /// Progress of a file transfer, reported through <see cref="System.IProgress{T}"/>.
    /// </summary>
    public readonly struct EftProgress
    {
        /// <summary>Creates a new progress value.</summary>
        public EftProgress(long bytesTransferred, long totalBytes)
        {
            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
        }

        /// <summary>Number of file bytes sent so far.</summary>
        public long BytesTransferred { get; }

        /// <summary>Total size of the file in bytes.</summary>
        public long TotalBytes { get; }

        /// <summary>Completion percentage between 0 and 100.</summary>
        public double Percentage => TotalBytes == 0 ? 100 : BytesTransferred * 100.0 / TotalBytes;
    }
}
