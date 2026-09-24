using System;

namespace EasyFileTransfer.Model
{
    /// <summary>
    /// Result of the legacy <see cref="EftClient.Send"/> method.
    /// </summary>
    [Obsolete("Returned only by the legacy EftClient.Send method. SendFileAsync throws EftException on failure instead.")]
    public class Response
    {
        /// <summary>1 on success, -1 on failure.</summary>
        public int status { get; set; }

        /// <summary>Human-readable result.</summary>
        public string description { get; set; } = string.Empty;
    }
}
