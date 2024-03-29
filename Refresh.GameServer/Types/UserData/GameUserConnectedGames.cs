using Realms;

namespace Refresh.GameServer.Types.UserData;

/// <summary>
/// Tracks the platforms and games the user has connected on.
/// </summary>
/// <remarks>
/// We store each platform and game in a boolean.
/// This is to make it portable for Postgres, which doesn't support lists like Realm does
/// They will be returned as a list in the API.
/// </remarks>
public partial class GameUserConnectedGames : IEmbeddedObject
{
    /// <summary>
    /// Whether or not the user has connected to this instance on a PS3.
    /// </summary>
    public bool PlatformPS3 { get; set; }
    /// <summary>
    /// Whether or not the user has connected to this instance on RPCS3.
    /// </summary>
    public bool PlatformRPCS3 { get; set; }
    /// <summary>
    /// Whether or not the user has connected to this instance on a Vita.
    /// </summary>
    public bool PlatformVita { get; set; }
    /// <summary>
    /// Whether or not the user has connected to this instance on a PSP.
    /// </summary>
    public bool PlatformPSP { get; set; }
    
    /// <summary>
    /// Whether or not the user has played LBP1 on this instance.
    /// </summary>
    public bool GameLBP1 { get; set; }
    /// <summary>
    /// Whether or not the user has played LBP2 on this instance.
    /// </summary>
    public bool GameLBP2 { get; set; }
    /// <summary>
    /// Whether or not the user has played LBP3 on this instance.
    /// </summary>
    public bool GameLBP3 { get; set; }
    /// <summary>
    /// Whether or not the user has played LBP Vita on this instance.
    /// </summary>
    public bool GameVita { get; set; }
    /// <summary>
    /// Whether or not the user has played LBP PSP on this instance.
    /// </summary>
    public bool GamePSP { get; set; }
    /// <summary>
    /// Whether or not the user has played a beta build on this instance.
    /// </summary>
    public bool GameBeta { get; set; }
}