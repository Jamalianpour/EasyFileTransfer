using System;
using System.Security.Cryptography;
using System.Text;

namespace EasyFileTransfer.Internal
{
    /// <summary>
    /// Wire format of protocol version 2. All integers are big-endian.
    /// <code>
    /// server -> client  hello    "EFT" | version:1 | status:1 | flags:1 | nonce:32
    /// client -> server  request  "EFT" | version:1 | proof:32 | nameLength:2 | name:UTF-8 | fileLength:8
    /// server -> client  status:1
    /// client -> server  file bytes (fileLength) | sha256:32
    /// server -> client  status:1
    /// </code>
    /// proof = HMAC-SHA256(key: UTF-8 access token, data: nonce), or zeros when no token is used.
    /// </summary>
    internal static class Protocol
    {
        public const byte Version = 2;
        public const int MagicLength = 3;
        public const int NonceLength = 32;
        public const int ProofLength = 32;
        public const int HashLength = 32;
        public const int MaxFileNameBytes = 1024;
        public const int BufferSize = 81920;
        public const byte FlagAuthRequired = 0x01;

        public const int HelloLength = MagicLength + 1 + 1 + 1 + NonceLength;
        public const int RequestHeaderLength = MagicLength + 1 + ProofLength + 2;

        public static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private static readonly byte[] Magic = { (byte)'E', (byte)'F', (byte)'T' };

        public static void WriteMagic(byte[] buffer, int offset) => Buffer.BlockCopy(Magic, 0, buffer, offset, MagicLength);

        public static bool HasMagic(byte[] buffer, int offset) =>
            buffer[offset] == Magic[0] && buffer[offset + 1] == Magic[1] && buffer[offset + 2] == Magic[2];

        public static byte[] CreateNonce()
        {
            var nonce = new byte[NonceLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            return nonce;
        }

        public static byte[] ComputeProof(string? accessToken, byte[] nonce)
        {
            if (accessToken is null)
            {
                return new byte[ProofLength];
            }

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(accessToken)))
            {
                return hmac.ComputeHash(nonce);
            }
        }

        /// <summary>Compares two byte arrays in time that does not depend on their contents.</summary>
        public static bool FixedTimeEquals(byte[] left, int leftOffset, byte[] right, int length)
        {
            if (right.Length != length || left.Length - leftOffset < length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < length; i++)
            {
                diff |= left[leftOffset + i] ^ right[i];
            }

            return diff == 0;
        }

        public static void WriteUInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        public static int ReadUInt16(byte[] buffer, int offset) => (buffer[offset] << 8) | buffer[offset + 1];

        public static void WriteInt64(byte[] buffer, int offset, long value)
        {
            for (int i = 7; i >= 0; i--)
            {
                buffer[offset + i] = (byte)value;
                value >>= 8;
            }
        }

        public static long ReadInt64(byte[] buffer, int offset)
        {
            long value = 0;
            for (int i = 0; i < 8; i++)
            {
                value = (value << 8) | buffer[offset + i];
            }

            return value;
        }
    }
}
