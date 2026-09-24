using System.Collections.Concurrent;
using System.Net;

namespace EasyFileTransfer.Tests;

/// <summary>An <see cref="EftServer"/> on a random loopback port with its own temp directory.</summary>
internal sealed class TestServer : IDisposable
{
    public TestServer(Action<EftServerOptions>? configure = null)
    {
        Root = Directory.CreateTempSubdirectory("eft-tests-").FullName;
        SaveDirectory = Path.Combine(Root, "inbox");
        var options = new EftServerOptions
        {
            SaveDirectory = SaveDirectory,
            Port = 0,
            BindAddress = IPAddress.Loopback,
            IdleTimeout = TimeSpan.FromSeconds(10),
        };
        configure?.Invoke(options);

        Server = new EftServer(options);
        Server.FileReceived += (_, e) => Received.Add(e);
        Server.TransferFailed += (_, e) => Failures.Add(e);
        Server.Start();
    }

    /// <summary>Parent of <see cref="SaveDirectory"/>; used to detect writes that escape it.</summary>
    public string Root { get; }

    public string SaveDirectory { get; }

    public EftServer Server { get; }

    public int Port => Server.LocalEndPoint!.Port;

    public BlockingCollection<FileReceivedEventArgs> Received { get; } = new();

    public BlockingCollection<TransferFailedEventArgs> Failures { get; } = new();

    public EftClient CreateClient(EftClientOptions? options = null) => new("127.0.0.1", Port, options);

    public TransferFailedEventArgs NextFailure()
    {
        Assert.True(Failures.TryTake(out var failure, TimeSpan.FromSeconds(15)), "Expected the server to report a failed transfer.");
        return failure!;
    }

    public string[] SavedFiles() => Directory.GetFiles(SaveDirectory).Select(Path.GetFileName).ToArray()!;

    public string[] AllFilesUnderRoot() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories);

    public void Dispose()
    {
        Server.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
