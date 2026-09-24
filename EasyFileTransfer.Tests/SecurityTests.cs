using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EasyFileTransfer.Internal;

namespace EasyFileTransfer.Tests;

public class SecurityTests
{
    [Theory]
    [InlineData("../escaped.txt")]
    [InlineData("..\\escaped.txt")]
    [InlineData("../../escaped.txt")]
    [InlineData("/tmp/escaped.txt")]
    [InlineData("C:\\escaped.txt")]
    [InlineData("sub/escaped.txt")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("a.txt:ads")]
    public void Path_traversal_is_rejected(string maliciousName)
    {
        using var server = new TestServer();
        using var raw = new RawClient(server.Port);
        raw.ReadHello();
        raw.SendRequest(maliciousName, 4);

        Assert.Equal(EftStatus.InvalidFileName, raw.ReadStatus());
        Assert.Equal(EftStatus.InvalidFileName, Assert.IsType<EftException>(server.NextFailure().Exception).Status);
        Assert.Empty(server.AllFilesUnderRoot());
    }

    [Fact]
    public void Invalid_utf8_file_name_is_rejected()
    {
        using var server = new TestServer();
        using var raw = new RawClient(server.Port);
        raw.ReadHello();
        raw.SendRequest(new byte[] { 0x61, 0xC0, 0xAF, 0x62 }, 4);

        Assert.Equal(EftStatus.InvalidFileName, raw.ReadStatus());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Protocol.MaxFileNameBytes + 1)]
    public void Out_of_range_name_length_is_rejected_before_allocating(int nameLength)
    {
        using var server = new TestServer();
        using var raw = new RawClient(server.Port);
        raw.ReadHello();
        var header = new byte[Protocol.RequestHeaderLength];
        Protocol.WriteMagic(header, 0);
        header[Protocol.MagicLength] = Protocol.Version;
        Protocol.WriteUInt16(header, Protocol.RequestHeaderLength - 2, nameLength);
        raw.Stream.Write(header);

        Assert.Equal(EftStatus.InvalidFileName, raw.ReadStatus());
    }

    [Fact]
    public void Negative_file_length_is_rejected()
    {
        using var server = new TestServer();
        using var raw = new RawClient(server.Port);
        raw.ReadHello();
        raw.SendRequest("x.bin", -1);

        Assert.Equal(EftStatus.ProtocolError, raw.ReadStatus());
    }

    [Fact]
    public void Unsupported_protocol_version_is_rejected()
    {
        using var server = new TestServer();
        using var raw = new RawClient(server.Port);
        raw.ReadHello();
        raw.SendRequest(Encoding.UTF8.GetBytes("x.bin"), 1, version: 99);

        Assert.Equal(EftStatus.UnsupportedVersion, raw.ReadStatus());
    }

    [Fact]
    public async Task Garbage_input_does_not_crash_the_server()
    {
        using var server = new TestServer();

        // The 0.1.x client's first packet, then random noise, then an abrupt disconnect.
        foreach (byte[] junk in new[] { "\u0002125" + "8\u0004file.txt", "EFT", "\u0002999abc" }.Select(Encoding.UTF8.GetBytes).Append(RandomNumberGenerator.GetBytes(4096)))
        {
            using var raw = new RawClient(server.Port);
            raw.ReadHello();
            raw.Stream.Write(junk);
            raw.Stream.Write(new byte[Protocol.RequestHeaderLength]);
            raw.ReadStatus();
        }

        await server.CreateClient().SendAsync(new MemoryStream("still alive"u8.ToArray()), "ok.txt");
        Assert.Equal(new[] { "ok.txt" }, server.SavedFiles());
    }

    [Fact]
    public void Wrong_hash_is_rejected_and_nothing_is_saved()
    {
        using var server = new TestServer();
        using (var raw = new RawClient(server.Port))
        {
            raw.ReadHello();
            raw.SendRequest("tampered.bin", 3);
            Assert.Equal(EftStatus.Success, raw.ReadStatus());
            raw.SendContent(new byte[] { 1, 2, 3 }, hash: new byte[32]);
            Assert.Equal(EftStatus.IntegrityCheckFailed, raw.ReadStatus());
        }

        server.NextFailure();
        Assert.Empty(Directory.EnumerateFileSystemEntries(server.SaveDirectory));
    }

    [Fact]
    public void Truncated_upload_leaves_no_partial_file()
    {
        using var server = new TestServer();
        using (var raw = new RawClient(server.Port))
        {
            raw.ReadHello();
            raw.SendRequest("partial.bin", 1_000_000);
            Assert.Equal(EftStatus.Success, raw.ReadStatus());
            raw.Stream.Write(new byte[1000]);
        }

        Assert.IsType<EndOfStreamException>(server.NextFailure().Exception);
        Assert.Empty(Directory.EnumerateFileSystemEntries(server.SaveDirectory));
    }

    [Fact]
    public async Task Unwritable_save_directory_reports_server_error()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return; // Permission bits are not enforced here.
        }

        using var server = new TestServer();
        File.SetUnixFileMode(server.SaveDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var ex = await Assert.ThrowsAsync<EftException>(() => server.CreateClient().SendAsync(new MemoryStream(new byte[1]), "x.bin"));
            Assert.Equal(EftStatus.ServerError, ex.Status);
        }
        finally
        {
            File.SetUnixFileMode(server.SaveDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task Max_file_size_is_enforced()
    {
        using var server = new TestServer(o => o.MaxFileSize = 100);
        var client = server.CreateClient();

        await client.SendAsync(new MemoryStream(new byte[100]), "fits.bin");
        var ex = await Assert.ThrowsAsync<EftException>(() => client.SendAsync(new MemoryStream(new byte[101]), "too-big.bin"));

        Assert.Equal(EftStatus.FileTooLarge, ex.Status);
        Assert.Equal(new[] { "fits.bin" }, server.SavedFiles());
    }

    [Fact]
    public void Absurd_file_size_is_rejected_for_lack_of_space()
    {
        using var server = new TestServer();
        using var raw = new RawClient(server.Port);
        raw.ReadHello();
        raw.SendRequest("huge.bin", long.MaxValue);

        Assert.Equal(EftStatus.InsufficientStorage, raw.ReadStatus());
    }

    [Fact]
    public async Task Idle_connections_are_dropped()
    {
        using var server = new TestServer(o => o.IdleTimeout = TimeSpan.FromMilliseconds(300));
        using var raw = new RawClient(server.Port);
        raw.ReadHello();

        Assert.IsType<TimeoutException>(server.NextFailure().Exception);
        Assert.Null(raw.ReadStatus());
        await server.CreateClient().SendAsync(new MemoryStream(new byte[1]), "after.bin");
    }

    [Fact]
    public async Task Connection_limit_is_enforced()
    {
        using var server = new TestServer(o => o.MaxConcurrentConnections = 1);
        using var hog = new RawClient(server.Port);
        Assert.Equal(EftStatus.Success, (EftStatus)hog.ReadHello()[Protocol.MagicLength + 1]);

        var ex = await Assert.ThrowsAsync<EftException>(() => server.CreateClient().SendAsync(new MemoryStream(new byte[1]), "x.bin"));
        Assert.Equal(EftStatus.ServerBusy, ex.Status);

        hog.Dispose();
        server.NextFailure(); // busy rejection
        server.NextFailure(); // hog disconnected
        await server.CreateClient().SendAsync(new MemoryStream(new byte[1]), "x.bin");
    }

    [Fact]
    public async Task Access_token_is_required_when_configured()
    {
        using var server = new TestServer(o => o.AccessToken = "correct horse battery staple");
        var content = new MemoryStream(new byte[10]);

        var missing = await Assert.ThrowsAsync<EftException>(() => server.CreateClient().SendAsync(content, "a.bin"));
        Assert.Equal(EftStatus.AuthenticationFailed, missing.Status);

        content.Position = 0;
        var wrong = await Assert.ThrowsAsync<EftException>(() =>
            server.CreateClient(new EftClientOptions { AccessToken = "wrong" }).SendAsync(content, "a.bin"));
        Assert.Equal(EftStatus.AuthenticationFailed, wrong.Status);

        content.Position = 0;
        await server.CreateClient(new EftClientOptions { AccessToken = "correct horse battery staple" }).SendAsync(content, "a.bin");
        Assert.Equal(new[] { "a.bin" }, server.SavedFiles());
    }

    [Fact]
    public void Proof_is_bound_to_the_servers_nonce()
    {
        using var server = new TestServer(o => o.AccessToken = "secret");
        byte[] replayedProof;
        using (var first = new RawClient(server.Port))
        {
            replayedProof = Protocol.ComputeProof("secret", RawClient.Nonce(first.ReadHello()));
        }

        using var second = new RawClient(server.Port);
        second.ReadHello();
        second.SendRequest("replay.bin", 1, replayedProof);

        Assert.Equal(EftStatus.AuthenticationFailed, second.ReadStatus());
    }

    [Fact]
    public async Task Tls_transfer_works_with_a_pinned_certificate()
    {
        using var certificate = CreateSelfSignedCertificate();
        using var server = new TestServer(o => o.ServerCertificate = certificate);
        var client = server.CreateClient(new EftClientOptions
        {
            UseTls = true,
            TlsTargetHost = "localhost",
            ServerCertificateValidationCallback = (_, cert, _, _) =>
                cert != null && cert.GetCertHashString() == certificate.Thumbprint,
        });

        await client.SendAsync(new MemoryStream("encrypted"u8.ToArray()), "secret.txt");

        Assert.Equal("encrypted", await File.ReadAllTextAsync(Path.Combine(server.SaveDirectory, "secret.txt")));
    }

    [Fact]
    public async Task Tls_client_rejects_an_untrusted_certificate_by_default()
    {
        using var certificate = CreateSelfSignedCertificate();
        using var server = new TestServer(o => o.ServerCertificate = certificate);
        var client = server.CreateClient(new EftClientOptions { UseTls = true, TlsTargetHost = "localhost" });

        await Assert.ThrowsAsync<AuthenticationException>(() => client.SendAsync(new MemoryStream(new byte[1]), "x.bin"));
        Assert.Empty(server.SavedFiles());
    }

    [Fact]
    public async Task Plaintext_client_cannot_talk_to_a_tls_server()
    {
        using var certificate = CreateSelfSignedCertificate();
        using var server = new TestServer(o =>
        {
            o.ServerCertificate = certificate;
            o.IdleTimeout = TimeSpan.FromSeconds(2);
        });
        var client = server.CreateClient(new EftClientOptions { Timeout = TimeSpan.FromSeconds(5) });

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendAsync(new MemoryStream(new byte[1]), "x.bin"));
        Assert.Empty(server.SavedFiles());
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        // Round-trip through PKCS#12 so the private key is usable by SslStream on every OS.
        byte[] pfx = ephemeral.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(pfx, null);
#else
        return new X509Certificate2(pfx);
#endif
    }
}
