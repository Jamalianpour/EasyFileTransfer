using System.Security.Cryptography;

namespace EasyFileTransfer.Tests;

public class TransferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(81920)]
    [InlineData(5 * 1024 * 1024 + 7)]
    public async Task Sends_file_intact(int size)
    {
        using var server = new TestServer();
        byte[] content = RandomNumberGenerator.GetBytes(size);
        string source = Path.Combine(server.Root, "source.bin");
        await File.WriteAllBytesAsync(source, content);

        var reports = new List<EftProgress>();
        await server.CreateClient().SendFileAsync(source, new SyncProgress(reports.Add));

        Assert.True(server.Received.TryTake(out var received, TimeSpan.FromSeconds(10)));
        Assert.Equal(Path.Combine(server.SaveDirectory, "source.bin"), received!.FilePath);
        Assert.Equal("source.bin", received.RequestedFileName);
        Assert.Equal(size, received.Length);
        Assert.Equal(content, await File.ReadAllBytesAsync(received.FilePath));
        Assert.Equal(new[] { "source.bin" }, server.SavedFiles());
        Assert.Equal(100, reports[^1].Percentage);
        Assert.Equal(size, reports[^1].BytesTransferred);
    }

    [Fact]
    public async Task Never_overwrites_an_existing_file()
    {
        using var server = new TestServer();
        await File.WriteAllTextAsync(Path.Combine(server.SaveDirectory, "a.txt"), "original");
        var client = server.CreateClient();

        await client.SendAsync(new MemoryStream("first"u8.ToArray()), "a.txt");
        await client.SendAsync(new MemoryStream("second"u8.ToArray()), "a.txt");

        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(server.SaveDirectory, "a.txt")));
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(server.SaveDirectory, "a (1).txt")));
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(server.SaveDirectory, "a (2).txt")));
    }

    [Fact]
    public async Task Handles_concurrent_transfers()
    {
        using var server = new TestServer();
        var client = server.CreateClient();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            client.SendAsync(new MemoryStream(RandomNumberGenerator.GetBytes(200_000)), $"file{i}.bin")));

        Assert.Equal(8, server.SavedFiles().Length);
    }

    [Fact]
    public async Task Cancellation_aborts_the_transfer_and_leaves_no_partial_file()
    {
        using var server = new TestServer();
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(p =>
        {
            if (p.BytesTransferred > 1_000_000)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            server.CreateClient().SendAsync(new MemoryStream(new byte[50_000_000]), "big.bin", progress, cts.Token));

        server.NextFailure();
        Assert.Empty(server.SavedFiles());
        Assert.Empty(Directory.GetFiles(server.SaveDirectory, "*", new EnumerationOptions { AttributesToSkip = 0 }));
    }

    [Fact]
    public async Task Server_can_be_stopped_and_restarted()
    {
        using var server = new TestServer();
        int port = server.Port;
        await server.Server.StopAsync();
        Assert.False(server.Server.IsRunning);

        server.Server.Options.Port = port;
        server.Server.Start();
        await server.CreateClient().SendAsync(new MemoryStream(new byte[10]), "after-restart.bin");
        Assert.Contains("after-restart.bin", server.SavedFiles());
    }

    [Fact]
    public async Task Client_rejects_unsafe_file_names_before_connecting()
    {
        var client = new EftClient("127.0.0.1", 1);
        var ex = await Assert.ThrowsAsync<EftException>(() => client.SendAsync(new MemoryStream(), "../evil.txt"));
        Assert.Equal(EftStatus.InvalidFileName, ex.Status);
    }

    [Fact]
    public async Task Client_times_out_against_a_silent_server()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var client = new EftClient("127.0.0.1", port, new EftClientOptions { Timeout = TimeSpan.FromMilliseconds(500) });

        await Assert.ThrowsAsync<TimeoutException>(() => client.SendAsync(new MemoryStream(new byte[1]), "x.bin"));
    }

#pragma warning disable CS0618 // Legacy API must keep working.
    [Fact]
    public async Task Legacy_api_still_works()
    {
        string root = Directory.CreateTempSubdirectory("eft-legacy-").FullName;
        int port = GetFreePort();
        var server = new EftServer(root + Path.DirectorySeparatorChar, port);
        var thread = new Thread(server.StartServer) { IsBackground = true };
        thread.Start();
        try
        {
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllTextAsync(source, "legacy");
            await WaitUntilAsync(() => server.IsRunning);

            Model.Response response = EftClient.Send(source, "127.0.0.1", port);

            Assert.Equal(1, response.status);
            Assert.Equal(100, EftClient.ProgressValue);
            Assert.Equal("legacy", await File.ReadAllTextAsync(Path.Combine(root, Path.GetFileName(source))));
            File.Delete(source);
        }
        finally
        {
            server.Dispose();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Legacy_send_reports_errors_instead_of_throwing()
    {
        Model.Response response = EftClient.Send("/does/not/exist.bin", "127.0.0.1", GetFreePort());
        Assert.Equal(-1, response.status);
        Assert.StartsWith("Error: ", response.description);
    }
#pragma warning restore CS0618

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
    }
}

internal sealed class SyncProgress(Action<EftProgress> handler) : IProgress<EftProgress>
{
    public void Report(EftProgress value) => handler(value);
}
