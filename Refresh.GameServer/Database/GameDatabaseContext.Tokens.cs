using System.Security.Cryptography;
using JetBrains.Annotations;
using Refresh.GameServer.Authentication;
using Refresh.GameServer.Types.UserData;

namespace Refresh.GameServer.Database;

public partial class GameDatabaseContext // Tokens
{
    private const int DefaultCookieLength = 128;
    private const int MaxBase64Padding = 4;
    private const int MaxGameCookieLength = 127;
    private const string GameCookieHeader = "MM_AUTH=";
    private static readonly int GameCookieLength;

    public const int DefaultTokenExpirySeconds = 86400; // 1 day
    public const int RefreshTokenExpirySeconds = 2678400; // 1 month
    public const int GameTokenExpirySeconds = 14400; // 4 hours
    
    static GameDatabaseContext()
    {
        // LBP cannot store tokens if >127 chars, calculate max possible length here
        GameCookieLength = (int)Math.Floor((MaxGameCookieLength - GameCookieHeader.Length - MaxBase64Padding) * 3 / 4.0);
    }
    
    private static string GetTokenString(int length)
    {
        byte[] tokenData = new byte[length];
        
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenData);

        return Convert.ToBase64String(tokenData);
    }
    
    public Token GenerateTokenForUser(GameUser user, TokenType type, TokenGame game, TokenPlatform platform, int tokenExpirySeconds = DefaultTokenExpirySeconds)
    {
        // TODO: JWT (JSON Web Tokens) for TokenType.Api
        
        int cookieLength = type == TokenType.Game ? GameCookieLength : DefaultCookieLength;

        Token token = new()
        {
            User = user,
            TokenData = GetTokenString(cookieLength),
            TokenType = type,
            TokenGame = game,
            TokenPlatform = platform,
            ExpiresAt = this._time.Now.AddSeconds(tokenExpirySeconds),
            LoginDate = this._time.Now,
        };
        
        if (user.LastLoginDate == DateTimeOffset.MinValue)
        {
            this.CreateUserFirstLoginEvent(user, user);
        }

        if (type == TokenType.Game)
        {
            this.UpdateConnectedGames(user, game, platform);
        }

        this._realm.Write(() =>
        {
            user.LastLoginDate = this._time.Now;
            this._realm.Add(token);
        });
        
        return token;
    }
    
    [Pure]
    [ContractAnnotation("=> canbenull")]
    public Token? GetTokenFromTokenData(string tokenData, TokenType type)
    {
        Token? token = this._realm.All<Token>()
            .FirstOrDefault(t => t.TokenData == tokenData && t._TokenType == (int)type);

        if (token == null) return null;

        // ReSharper disable once InvertIf
        if (token.ExpiresAt < this._time.Now)
        {
            this.RevokeToken(token);
            return null;
        }

        return token;
    }

    [Pure]
    [ContractAnnotation("=> canbenull")]
    public GameUser? GetUserFromTokenData(string tokenData, TokenType type) => 
        this.GetTokenFromTokenData(tokenData, type)?.User;

    public void SetUserPassword(GameUser user, string? passwordBcrypt, bool shouldReset = false)
    {
        this._realm.Write(() =>
        {
            user.PasswordBcrypt = passwordBcrypt;
            user.ShouldResetPassword = shouldReset;
        });
    }

    public bool RevokeTokenByTokenData(string? tokenData, TokenType type)
    {
        if (tokenData == null) return false;

        Token? token = this._realm.All<Token>().FirstOrDefault(t => t.TokenData == tokenData && t._TokenType == (int)type);
        if (token == null) return false;

        this.RevokeToken(token);

        return true;
    }

    public void RevokeToken(Token token)
    {
        this._realm.Write(() =>
        {
            this._realm.Remove(token);
        });
    }

    public void RevokeAllTokensForUser(GameUser user)
    {
        this._realm.Write(() =>
        {
            this._realm.RemoveRange(this._realm.All<Token>().Where(t => t.User == user));
        });
    }

    public void AddIpVerificationRequest(GameUser user, string ipAddress)
    {
        GameIpVerificationRequest request = new()
        {
            IpAddress = ipAddress,
            CreatedAt = this._time.Now,
        };

        this._realm.Write(() =>
        {
            user.IpVerificationRequests.Add(request);
        });
    }

    public void SetApprovedIp(GameUser user, string ipAddress)
    {
        this._realm.Write(() =>
        {
            user.CurrentVerifiedIp = ipAddress;
            user.IpVerificationRequests.Clear();
        });
    }

    public void DenyIpVerificationRequest(GameUser user, string ipAddress)
    {
        IEnumerable<GameIpVerificationRequest> requests = user.IpVerificationRequests.Where(r => r.IpAddress == ipAddress);
        this._realm.Write(() =>
        {
            foreach (GameIpVerificationRequest request in requests)
            {
                user.IpVerificationRequests.Remove(request);
            }
        });
    }

    public DatabaseList<GameIpVerificationRequest> GetIpVerificationRequestsForUser(GameUser user, int count, int skip) 
        => new(user.IpVerificationRequests, skip, count);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user">The user's connected games to fill in.</param>
    /// <param name="game">The game the user is playing.</param>
    /// <param name="platform">The platform the user is connecting from.</param>
    /// <exception cref="InvalidOperationException">The game or platform is invalid.</exception>
    private void UpdateConnectedGames(GameUser user, TokenGame game, TokenPlatform platform)
    {
        this._realm.Write(() =>
        {
            switch (platform)
            {
                case TokenPlatform.PS3:
                    user.ConnectedGames.PlatformPS3 = true;
                    break;
                case TokenPlatform.RPCS3:
                    user.ConnectedGames.PlatformRPCS3 = true;
                    break;
                case TokenPlatform.Vita:
                    user.ConnectedGames.PlatformVita = true;
                    break;
                case TokenPlatform.PSP:
                    user.ConnectedGames.PlatformPSP = true;
                    break;
                case TokenPlatform.Website:
                default:
                    throw new InvalidOperationException($"Cannot set connected game for invalid platform {platform}");
            }

            switch (game)
            {
                case TokenGame.LittleBigPlanet1:
                    user.ConnectedGames.GameLBP1 = true;
                    break;
                case TokenGame.LittleBigPlanet2:
                    user.ConnectedGames.GameLBP2 = true;
                    break;
                case TokenGame.LittleBigPlanet3:
                    user.ConnectedGames.GameLBP3 = true;
                    break;
                case TokenGame.LittleBigPlanetVita:
                    user.ConnectedGames.GameVita = true;
                    break;
                case TokenGame.LittleBigPlanetPSP:
                    user.ConnectedGames.GamePSP = true;
                    break;
                case TokenGame.BetaBuild:
                    user.ConnectedGames.GameBeta = true;
                    break;
                case TokenGame.Website:
                default:
                    throw new InvalidOperationException($"Cannot set connected game for invalid game {game}");
            }
        });
    }
}