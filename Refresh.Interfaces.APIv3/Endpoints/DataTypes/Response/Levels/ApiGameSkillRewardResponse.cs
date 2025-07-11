﻿using Refresh.Core.Types.Data;
using Refresh.Database.Models.Levels;

namespace Refresh.Interfaces.APIv3.Endpoints.DataTypes.Response.Levels;

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class ApiGameSkillRewardResponse : IApiResponse, IDataConvertableFrom<ApiGameSkillRewardResponse, GameSkillReward>
{
    public int Id { get; set; }
    public bool Enabled { get; set; }
    public string? Title { get; set; }
    public float RequiredAmount { get; set; }
    public GameSkillRewardCondition ConditionType { get; set; }

    public static ApiGameSkillRewardResponse? FromOld(GameSkillReward? old, DataContext dataContext)
    {
        if (old == null) return null;
        
        return new ApiGameSkillRewardResponse
        {
            Id = old.Id,
            Enabled = old.Enabled,
            Title = old.Title,
            RequiredAmount = old.RequiredAmount,
            ConditionType = old.ConditionType,
        };
    }

    public static IEnumerable<ApiGameSkillRewardResponse> FromOldList(IEnumerable<GameSkillReward> oldList,
        DataContext dataContext) => oldList.Select(old => FromOld(old, dataContext)).ToList()!;
}