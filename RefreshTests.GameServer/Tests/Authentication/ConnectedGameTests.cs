using Refresh.GameServer.Types.UserData;

namespace RefreshTests.GameServer.Tests.Authentication;

public class ConnectedGameTests : GameServerTest
{
    [Test]
    public void TracksNewConnectedGame()
    {
        using TestContext context = this.GetServer();
        GameUser user = context.CreateUser();
        
        Assert.Multiple(() =>
        {
            Assert.That(user.ConnectedGames.GameLBP2, Is.False);
            Assert.That(user.ConnectedGames.PlatformPS3, Is.False);
        });

        context.CreateToken(user);
        
        Assert.Multiple(() =>
        {
            Assert.That(user.ConnectedGames.GameLBP2, Is.True);
            Assert.That(user.ConnectedGames.PlatformPS3, Is.True);
        });
    }
}