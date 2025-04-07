using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Refresh.Common.Authentication;

namespace Refresh.Common.Rooms;

public class GameRoom
{
    public GameRoom(GameRoomPlayer host, TokenPlatform platform, TokenGame game, NatType natType, bool? passedNoJoinPoint)
    {
        this.PlayerIds.Add(host);
        this.Platform = platform;
        this.Game = game;
        this.NatType = natType;
        this.PassedNoJoinPoint = passedNoJoinPoint ?? false;
    }

    public readonly ObjectId RoomId = ObjectId.GenerateNewId();
    
    public readonly List<GameRoomPlayer> PlayerIds = new(4);
    public GameRoomPlayer HostId => this.PlayerIds[0];

    public readonly TokenPlatform Platform;
    public readonly TokenGame Game;

    public readonly NatType NatType;

    public DateTimeOffset LastContact;

    public bool PassedNoJoinPoint;

    public bool IsExpired => DateTimeOffset.Now > this.LastContact + TimeSpan.FromMinutes(3) || this.PlayerIds.Count == 0;

    [JsonProperty("State"), JsonConverter(typeof(StringEnumConverter))]
    public RoomState RoomState;
    [JsonProperty("Mood"), JsonConverter(typeof(StringEnumConverter))]
    public RoomMood RoomMood;
    [JsonProperty, JsonConverter(typeof(StringEnumConverter))]
    public RoomSlotType LevelType = RoomSlotType.Pod;
    
    [JsonProperty] public int LevelId;
}