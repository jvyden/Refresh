using Bunkum.Core.Storage;
using Bunkum.Protocols.Http.Direct;
using Microsoft.Extensions.Time.Testing;
using NotEnoughLogs;
using RefreshTests.GameServer.Logging;

namespace RefreshTests.GameServer.GameServer;

[Parallelizable]
[Timeout(2000)]
public class GameServerTest
{
    protected static readonly Logger Logger = new(new []
    {
        new NUnitSink(),
    });
    
    // ReSharper disable once MemberCanBeMadeStatic.Global
    protected TestContext GetServer(bool startServer = true, IDataStore? dataStore = null)
    {
        DirectHttpListener listener = new(Logger);
        HttpClient client = listener.GetClient();
        FakeTimeProvider time = new();

        TestGameDatabaseProvider provider = new(time);

        Lazy<TestRefreshGameServer> gameServer = new(() =>
        {
            TestRefreshGameServer gameServer = new(listener, () => provider, dataStore);
            gameServer.Start();

            return gameServer;
        });

        if (startServer) _ = gameServer.Value;
        else provider.Initialize();

        return new TestContext(gameServer, provider.GetContext(), client, listener, time);
    }
}