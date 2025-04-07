using Refresh.Common.Rooms;
using Refresh.GameServer.Database;
using Refresh.GameServer.Types.UserData;

namespace Refresh.GameServer.Types.Matching;

public static class GameRoomExtensions
{
    public static List<GameUser?> GetPlayers(this GameRoom room, GameDatabaseContext database) =>
        room.PlayerIds
            .Where(i => i.Id != null)
            .Select(i => database.GetUserByObjectId(i.Id))
            .ToList();

    public static GameUser? GetHost(this GameRoom room, GameDatabaseContext database)
    {
        if (room.PlayerIds[0].Id == null) return null;
        return database.GetUserByObjectId(room.PlayerIds[0].Id);
    }
}