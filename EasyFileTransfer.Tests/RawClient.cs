using System.Net.Sockets;
using System.Security.Cryptography;
using EasyFileTransfer.Internal;

namespace EasyFileTransfer.Tests;

/// <summary>Speaks the wire protocol by hand so tests can send what a real client never would.</summary>
internal sealed class RawClient : IDisposable
{
    private readonly TcpClient _tcp;

    public RawClient(int port)
    {
        _tcp = new TcpClient();
        _tcp.Connect("127.0.0.1", port);
        Stream = _tcp.GetStream();
        Stream.ReadTimeout = 15000;
    }

    public NetworkStream Stream { get; }

    public byte[] ReadHello()
    {
        var hello = new byte[Protocol.HelloLength];
        Stream.ReadExactly(hello);
        return hello;
    }

    public static byte[] Nonce(byte[] hello) => hello.AsSpan(Protocol.MagicLength + 3, Protocol.NonceLength).ToArray();

    public void SendRequest(byte[] nameBytes, long length, byte[]? proof = null, byte version = Protocol.Version)
    {
        var request = new byte[Protocol.RequestHeaderLength + nameBytes.Length + 8];
        Protocol.WriteMagic(request, 0);
        request[Protocol.MagicLength] = version;
        (proof ?? new byte[Protocol.ProofLength]).CopyTo(request, Protocol.MagicLength + 1);
        Protocol.WriteUInt16(request, Protocol.RequestHeaderLength - 2, nameBytes.Length);
        nameBytes.CopyTo(request, Protocol.RequestHeaderLength);
        Protocol.WriteInt64(request, Protocol.RequestHeaderLength + nameBytes.Length, length);
        Stream.Write(request);
    }

    public void SendRequest(string name, long length, byte[]? proof = null) =>
        SendRequest(Protocol.StrictUtf8.GetBytes(name), length, proof);

    public void SendContent(byte[] content, byte[]? hash = null)
    {
        Stream.Write(content);
        Stream.Write(hash ?? SHA256.HashData(content));
    }

    /// <summary>Reads one status byte, or returns null if the server closed the connection.</summary>
    public EftStatus? ReadStatus()
    {
        try
        {
            int b = Stream.ReadByte();
            return b < 0 ? null : (EftStatus)b;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _tcp.Dispose();
}
