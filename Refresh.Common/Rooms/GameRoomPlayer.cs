using MongoDB.Bson;

namespace Refresh.Common.Rooms;

public record GameRoomPlayer(string Username, ObjectId? Id);