using System;

namespace EasyFileTransfer.Internal
{
    /// <summary>
    /// Decides whether a file name received from the network is safe to create inside the
    /// save directory. The rules are the union of Windows and Unix restrictions so a name
    /// accepted on one platform is accepted on all of them.
    /// </summary>
    internal static class FileNameValidator
    {
        public const int MaxLength = 255;

        private static readonly char[] ForbiddenChars = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
        };

        public static bool IsValid(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || name!.Length > MaxLength)
            {
                return false;
            }

            // Windows silently strips trailing dots and spaces, which would let "a.txt." alias "a.txt";
            // this also rejects "." and "..".
            char last = name[name.Length - 1];
            if (last == '.' || last == ' ')
            {
                return false;
            }

            foreach (char c in name)
            {
                if (c < 0x20 || c == 0x7F || Array.IndexOf(ForbiddenChars, c) >= 0)
                {
                    return false;
                }
            }

            int dot = name.IndexOf('.');
            string stem = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' ');
            foreach (string reserved in ReservedNames)
            {
                if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
