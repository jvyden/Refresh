using Microsoft.Extensions.Time.Testing;
using Refresh.GameServer.Database;

namespace RefreshTests.GameServer.GameServer;

public class TestGameDatabaseProvider : GameDatabaseProvider
{
    public TestGameDatabaseProvider(FakeTimeProvider time) : base(time)
    {}

    private readonly int _databaseId = Random.Shared.Next();
    protected override string Filename => $"realm-inmemory-{this._databaseId}";
    protected override bool InMemory => true;
}