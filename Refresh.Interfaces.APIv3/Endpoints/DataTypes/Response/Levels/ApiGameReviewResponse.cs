using Refresh.Core.Types.Data;
using Refresh.Database.Models.Comments;
using Refresh.Interfaces.APIv3.Endpoints.DataTypes.Response.Users;

namespace Refresh.Interfaces.APIv3.Endpoints.DataTypes.Response.Levels;

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ApiGameReviewResponse : IApiResponse, IDataConvertableFrom<ApiGameReviewResponse, GameReview>
{
    public required int ReviewId { get; set; }
    public required ApiGameLevelResponse Level { get; set; }
    public required ApiGameUserResponse Publisher { get; set; }
    public required DateTimeOffset PostedAt { get; set; }
    public required string Labels { get; set; }
    public required string Text { get; set; }
    public static ApiGameReviewResponse? FromOld(GameReview? old, DataContext dataContext)
    {
        if (old == null) return null;
        return new ApiGameReviewResponse
        {
            ReviewId = old.ReviewId,
            Level = ApiGameLevelResponse.FromOld(old.Level, dataContext)!,
            Publisher = ApiGameUserResponse.FromOld(old.Publisher, dataContext)!,
            PostedAt = old.PostedAt,
            Labels = old.Labels,
            Text = old.Content,
        };
    }

    public static IEnumerable<ApiGameReviewResponse> FromOldList(IEnumerable<GameReview> oldList,
        DataContext dataContext) => oldList.Select(old => FromOld(old, dataContext)).ToList()!;
}