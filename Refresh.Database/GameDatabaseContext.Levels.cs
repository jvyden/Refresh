using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;
using Refresh.Common.Constants;
using Refresh.Common.Helpers;
using Refresh.Database.Query;
using Refresh.Database.Models.Authentication;
using Refresh.Database.Models.Activity;
using Refresh.Database.Models.Users;
using Refresh.Database.Models.Levels.Challenges;
using Refresh.Database.Models.Levels.Scores;
using Refresh.Database.Models.Levels;
using Refresh.Database.Models.Relations;

namespace Refresh.Database;

public partial class GameDatabaseContext // Levels
{
    private IQueryable<GameLevel> GameLevelsIncluded => this.GameLevels
        .Include(l => l.Publisher);

    private IQueryable<GameSkillReward> SkillRewardsIncluded => this.GameSkillRewards
        .Include(s => s.Level)
        .Include(s => s.Level.Publisher);
    
    public bool AddLevel(GameLevel level)
    {
        if (level.Title is { Length: > UgcLimits.TitleLimit })
            level.Title = level.Title[..UgcLimits.TitleLimit];

        if (level.Description is { Length: > UgcLimits.DescriptionLimit })
            level.Description = level.Description[..UgcLimits.DescriptionLimit];
        
        if (level.Publisher == null) throw new InvalidOperationException("Cannot create a level without a publisher");

        DateTimeOffset timestamp = this._time.Now;
        this.AddSequentialObject(level, () =>
        {
            level.PublishDate = timestamp;
            level.UpdateDate = timestamp;
        });

        return true;
    }

    public GameLevel GetStoryLevelById(int id)
    {
        GameLevel? level = this.GameLevelsIncluded
            .FirstOrDefault(l => l.StoryId == id);

        if (level != null) return level;
        
        //Create a new level for the story level
        level = new()
        {
            Title = $"Story level #{id}",
            Publisher = null,
            StoryId = id,
        };
            
        //Add the new story level to the database
        DateTimeOffset timestamp = this._time.Now;
        this.AddSequentialObject(level, () =>
        {
            level.PublishDate = timestamp;
            level.UpdateDate = timestamp;
        });
        
        return level;
    }

    public GameLevel UpdateLevelPublisher(GameLevel level, GameUser newAuthor)
    {
        if (level.Publisher?.UserId == newAuthor.UserId)
            return level;
        
        this.Write(() =>
        {
            // Change the level's publisher, making sure we also unset OriginalPublisher
            // if this level wasn't uploaded by an actual user originally.
            level.Publisher = newAuthor;
            level.OriginalPublisher = null;
        });

        return level;
    }

    public void UpdateLevelLocations(IEnumerable<ISerializedEditLevelLocation> locations, GameUser updatingUser)
    {
        IEnumerable<GameLevel> levelsByUser = this.GameLevels.Where(l => l.Publisher != null && l.Publisher == updatingUser);
        int failedUpdates = 0;

        this.Write(() => 
        {
            foreach (ISerializedEditLevelLocation location in locations)
            {
                // This gets the level to update while also verifying whether the user may even update its location
                GameLevel? level = levelsByUser.FirstOrDefault(l => l.LevelId == location.LevelId);

                if (level != null)
                {
                    level.LocationX = location.Location.X;
                    level.LocationY = location.Location.Y;
                }
                else 
                {
                    failedUpdates++;
                }
            }
        });

        // Notify the user about how many of the location updates have failed
        if (failedUpdates > 0)
        {
            this.AddErrorNotification("Level updates failed", $"Failed to update {failedUpdates} out of {locations.Count()} level locations.", updatingUser);
        }
    }
    
    public GameLevel? UpdateLevel(GameLevel newLevel, GameUser author)
    {
        if (newLevel.Title is { Length: > UgcLimits.TitleLimit })
            newLevel.Title = newLevel.Title[..UgcLimits.TitleLimit];

        if (newLevel.Description is { Length: > UgcLimits.DescriptionLimit })
            newLevel.Description = newLevel.Description[..UgcLimits.DescriptionLimit];
        
        // Verify if this level is able to be republished
        GameLevel? oldLevel = this.GetLevelById(newLevel.LevelId);
        if (oldLevel == null) return null;
            
        Debug.Assert(oldLevel.Publisher != null);
        if (oldLevel.Publisher.UserId != author.UserId) return null;
        
        // All checks passed, let's start by retaining some information from the old level
        newLevel.Publisher = author;
        newLevel.PublishDate = oldLevel.PublishDate;
        newLevel.DateTeamPicked = oldLevel.DateTeamPicked;
        
        // If the actual contents of the level haven't changed, extract some extra information
        if (oldLevel.RootResource == newLevel.RootResource)
        {
            newLevel.GameVersion = oldLevel.GameVersion;
            newLevel.UpdateDate = oldLevel.UpdateDate;
        }
        // If we're changing the actual level, update other things
        else
        {
            newLevel.UpdateDate = this._time.Now; // Set the last modified date
        }
        
        // Now newLevel is set up to replace oldLevel.
        // If information is lost here, then that's probably a bug.
        // Update the level's properties in the database
        this.Write(() =>
        {
            PropertyInfo[] userProps = typeof(GameLevel).GetProperties();
            foreach (PropertyInfo prop in userProps)
            {
                if (!prop.CanWrite || !prop.CanRead) continue;
                prop.SetValue(oldLevel, prop.GetValue(newLevel));
            }
        });

        return oldLevel;
    }
    
    public GameLevel? UpdateLevel(IApiEditLevelRequest body, GameLevel level)
    {
        if (body.Title is { Length: > UgcLimits.TitleLimit })
            body.Title = body.Title[..UgcLimits.TitleLimit];

        if (body.Description is { Length: > UgcLimits.DescriptionLimit })
            body.Description = body.Description[..UgcLimits.DescriptionLimit];
        
        this.Write(() =>
        {
            PropertyInfo[] userProps = body.GetType().GetProperties();
            foreach (PropertyInfo prop in userProps)
            {
                if (!prop.CanWrite || !prop.CanRead) continue;
                
                object? propValue = prop.GetValue(body);
                if(propValue == null) continue;

                PropertyInfo? gameLevelProp = level.GetType().GetProperty(prop.Name);
                Debug.Assert(gameLevelProp != null, $"Invalid property {prop.Name} on {nameof(IApiEditLevelRequest)}");
                
                gameLevelProp.SetValue(level, prop.GetValue(body));
            }
            
            level.UpdateDate = this._time.Now;
        });

        return level;
    }

    public void DeleteLevel(GameLevel level)
    {
        this.Write(() =>
        {
            IQueryable<Event> levelEvents = this.Events
                .Where(e => e._StoredDataType == (int)EventDataType.Level && e.StoredSequentialId == level.LevelId);
            
            this.Events.RemoveRange(levelEvents);

            this.FavouriteLevelRelations.RemoveRange(r => r.Level == level);
            this.PlayLevelRelations.RemoveRange(r => r.Level == level);
            this.QueueLevelRelations.RemoveRange(r => r.Level == level);
            this.RateLevelRelations.RemoveRange(r => r.Level == level);
            this.UniquePlayLevelRelations.RemoveRange(r => r.Level == level);
            this.TagLevelRelations.RemoveRange(r => r.Level == level);
            this.GameReviews.RemoveRange(r => r.Level == level);
            this.LevelPlaylistRelations.RemoveRange(r => r.Level == level);
            
            IEnumerable<GameChallenge> challenges = this.GameChallenges.Where(c => c.Level == level);

            foreach (GameChallenge challenge in challenges)
            {
                this.GameChallengeScores.RemoveRange(s => s.Challenge == challenge);
            }
            this.GameChallenges.RemoveRange(challenges);
            
            IQueryable<GameSubmittedScore> scores = this.GameSubmittedScores.Where(r => r.Level == level);
            
            foreach (GameSubmittedScore score in scores)
            {
                IQueryable<Event> scoreEvents = this.Events
                    .Where(e => e._StoredDataType == (int)EventDataType.Score && e.StoredObjectId == score.ScoreId);
                this.Events.RemoveRange(scoreEvents);
            }
            
            this.GameSubmittedScores.RemoveRange(scores);
        });

        //do in separate transaction in a vain attempt to fix Weirdness with favourite level relations having missing levels
        this.Write(() =>
        {
            this.GameLevels.Remove(level);
        });
    }
    
    private IQueryable<GameLevel> GetLevelsByGameVersion(TokenGame gameVersion) 
        => this.GameLevelsIncluded
            .Where(l => l.StoryId == 0) // Filter out any user levels
            .FilterByGameVersion(gameVersion);

    [Pure]
    public DatabaseList<GameLevel> GetLevelsByUser(GameUser user, int count, int skip, LevelFilterSettings levelFilterSettings, GameUser? accessor)
    {
        IEnumerable<GameLevel> levels;

        if (user.Username == SystemUsers.DeletedUserName)
        {
            levels = this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
                .FilterByLevelFilterSettings(accessor, levelFilterSettings)
                .Where(l => l.Publisher == null);
        }
        else if (user.Username == SystemUsers.UnknownUserName)
        {
            levels = this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
                .FilterByLevelFilterSettings(null, levelFilterSettings)
                .Where(l => l.IsReUpload && string.IsNullOrEmpty(l.OriginalPublisher));
        }
        else if (user.Username.StartsWith(SystemUsers.SystemPrefix))
        {
            string withoutPrefix = user.Username[1..];
            levels = this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
                .FilterByLevelFilterSettings(accessor, levelFilterSettings)
                .Where(l => l.OriginalPublisher == withoutPrefix);
        }
        else
        {
            levels = this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
                .FilterByLevelFilterSettings(accessor, levelFilterSettings)
                .Where(l => l.Publisher == user);
        }
        
        return new(levels.OrderByDescending(l => l.UpdateDate), skip, count);
    }
    
    public int GetTotalLevelsByUser(GameUser user) => this.GameLevels.Count(l => l.Publisher == user);
    
    [Pure]
    public DatabaseList<GameLevel> GetUserLevelsChunk(int skip, int count)
        => new(this.GameLevelsIncluded
            .Where(l => l.StoryId == 0), skip, count);

    [Pure]
    public IQueryable<GameLevel> GetAllUserLevels()
        => this.GameLevelsIncluded
            .Where(l => l.StoryId == 0);
    
    [Pure]
    public DatabaseList<GameLevel> GetNewestLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings) =>
        new(this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
            .FilterByLevelFilterSettings(user, levelFilterSettings)
            .OrderByDescending(l => l.PublishDate), skip, count);
    
    [Pure]
    public DatabaseList<GameLevel> GetRandomLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings)
    {
#if POSTGRES
        float seed = MathHelper.RemapIntToFloat(levelFilterSettings.Seed ?? 0);
        
        // TODO: include publisher in result somehow
        // this needs a hacky workaround, because Include causes an ORDER BY to be added after the joins
        // that breaks our query since we use ORDER BY in the sql and cant really use it after the fact
        // issue ref: https://github.com/dotnet/efcore/issues/29171
        // i've spent far too long trying to figure this out so i'll leave it for now

        // for solutions to the above that do multiple queries, cap it to a reasonable amount please
        // count = Math.Min(30, count);

        IQueryable<GameLevel> list = this.GameLevels
            .FromSqlInterpolated($"""
                                  SELECT
                                    setseed({seed}),
                                    RANDOM() as rand,
                                    *
                                  FROM "GameLevels"
                                  ORDER BY rand
                                  """)
            .AsNoTracking()
            .FilterByGameVersion(levelFilterSettings.GameVersion)
            .FilterByLevelFilterSettings(user, levelFilterSettings);

        return new DatabaseList<GameLevel>(list, skip, count);
#else
        Random random = new(levelFilterSettings.Seed ?? 0);
        
        return new DatabaseList<GameLevel>(this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
            .FilterByLevelFilterSettings(user, levelFilterSettings)
            .AsEnumerable()
            .OrderBy(_ => random.Next())
            , skip, count);
#endif
    }

    // TODO: reduce code duplication for getting most of x
    [Pure]
    public DatabaseList<GameLevel> GetMostHeartedLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings)
    {
        IQueryable<FavouriteLevelRelation> favourites = this.FavouriteLevelRelations;
        
        IEnumerable<GameLevel> mostHeartedLevels = favourites
            .Include(r => r.Level.Publisher)
            .AsEnumerableIfRealm()
            .GroupBy(r => r.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => x.Level)
            .Where(l => l != null)
            .Where(l => l.StoryId == 0)
            .FilterByGameVersion(levelFilterSettings.GameVersion)
            .AsEnumerable()
            .FilterByLevelFilterSettings(user, levelFilterSettings);

        return new DatabaseList<GameLevel>(mostHeartedLevels, skip, count);
    }
    
    [Pure]
    public DatabaseList<GameLevel> GetLevelsByTag(int count, int skip, GameUser? user, Tag tag, LevelFilterSettings levelFilterSettings)
    {
        IQueryable<TagLevelRelation> tagRelations = this.TagLevelRelations;
        
        IEnumerable<GameLevel> filteredTaggedLevels = tagRelations
            .Include(x => x.Level.Publisher)
            .Where(x => x._Tag == (int)tag)
            .AsEnumerableIfRealm()
            .Select(x => x.Level)
            .Distinct()
            .Where(l => l.StoryId == 0)
            .OrderByDescending(l => l.PublishDate)
            .FilterByLevelFilterSettings(user, levelFilterSettings)
            .FilterByGameVersion(levelFilterSettings.GameVersion);

        return new DatabaseList<GameLevel>(filteredTaggedLevels, skip, count);
    }
    
    [Pure]
    public DatabaseList<GameLevel> GetMostUniquelyPlayedLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings)
    {
        IQueryable<UniquePlayLevelRelation> uniquePlays = this.UniquePlayLevelRelations;
        
        IEnumerable<GameLevel> mostPlayed = uniquePlays
            .Include(r => r.Level.Publisher)
            .AsEnumerableIfRealm()
            .GroupBy(r => r.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => x.Level)
            .Where(l => l != null)
            .Where(l => l.StoryId == 0)
            .FilterByGameVersion(levelFilterSettings.GameVersion)
            .AsEnumerable()
            .FilterByLevelFilterSettings(user, levelFilterSettings);

        return new DatabaseList<GameLevel>(mostPlayed, skip, count);
    }
    
    [Pure]
    public DatabaseList<GameLevel> GetMostReplayedLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings)
    {
        IQueryable<PlayLevelRelation> plays = this.PlayLevelRelations;
        
        IEnumerable<GameLevel> mostPlayed = plays
            .Include(r => r.Level.Publisher)
            .AsEnumerableIfRealm()
            .GroupBy(r => r.Level)
            .Select(g => new { Level = g.Key, Count = g.Sum(p => p.Count) })
            .OrderByDescending(x => x.Count)
            .Select(x => x.Level)
            .Where(l => l != null)
            .FilterByGameVersion(levelFilterSettings.GameVersion)
            .AsEnumerable()
            .FilterByLevelFilterSettings(user, levelFilterSettings);

        return new DatabaseList<GameLevel>(mostPlayed, skip, count);
    }
    
    [Pure]
    public DatabaseList<GameLevel> GetHighestRatedLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings)
    {
        IQueryable<RateLevelRelation> ratings = this.RateLevelRelations;

        IEnumerable<GameLevel> highestRated = ratings
            .Include(r => r.Level.Publisher)
            .AsEnumerableIfRealm()
            .GroupBy(r => r.Level)
            .Select(g => new { Level = g.Key, Karma = g.Sum(r => r._RatingType) })
            .OrderByDescending(x => x.Karma) // reddit moment
            .Select(x => x.Level)
            .Where(l => l != null)
            .Where(l => l.StoryId == 0)
            .FilterByGameVersion(levelFilterSettings.GameVersion)
            .AsEnumerable()
            .FilterByLevelFilterSettings(user, levelFilterSettings);

        return new DatabaseList<GameLevel>(highestRated, skip, count);
    }
    
    [Pure]
    public DatabaseList<GameLevel> GetTeamPickedLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings) =>
        new(this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
            .Where(l => l.DateTeamPicked != null)
            .FilterByLevelFilterSettings(user, levelFilterSettings)
            .OrderByDescending(l => l.DateTeamPicked), skip, count);

    [Pure]
    public DatabaseList<GameLevel> GetDeveloperLevels(int count, int skip, LevelFilterSettings levelFilterSettings) =>
        new(this.GameLevels
            .Where(l => l.StoryId != 0) // filter to only levels with a story ID set
            .FilterByLevelFilterSettings(null, levelFilterSettings)
            .OrderByDescending(l => l.Title), skip, count);
    
    [Pure]
    public DatabaseList<GameLevel> GetCoolLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings) =>
        new(this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
            .FilterByLevelFilterSettings(user, levelFilterSettings)
            .Where(l => l.Score > 0)
            .OrderByDescending(l => l.Score), skip, count);

    [Pure]
    public DatabaseList<GameLevel> GetAdventureLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings) =>
        new(this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
            .FilterByLevelFilterSettings(user, levelFilterSettings)
            .Where(l => l.IsAdventure)
            .OrderByDescending(l => l.PublishDate), skip, count);

    [Pure]
    public DatabaseList<GameLevel> SearchForLevels(int count, int skip, GameUser? user, LevelFilterSettings levelFilterSettings, string query)
    {
        IQueryable<GameLevel> validLevels = this.GetLevelsByGameVersion(levelFilterSettings.GameVersion)
                .FilterByLevelFilterSettings(user, levelFilterSettings);

#if !POSTGRES
        List<GameLevel> levels = validLevels.Where(l =>
            QueryMethods.FullTextSearch(l.Title, query) ||
            QueryMethods.FullTextSearch(l.Description, query)
        ).ToList();
#else
        string dbQuery = $"%{query}%";
        List<GameLevel> levels = validLevels.Where(l =>
            EF.Functions.ILike(l.Title, dbQuery) ||
            EF.Functions.ILike(l.Description, dbQuery)
        ).ToList();
#endif
        
        // If the search is just an int, then we should also look for levels which match that ID
        if (int.TryParse(query, out int id))
        {
            // Try to find a level with the ID
            GameLevel? idLevel = validLevels.FirstOrDefault(l => l.LevelId == id);

            // If we found it, and it does not duplicate, add it
            if (idLevel != null && !levels.Contains(idLevel))
            {
                levels.Add(idLevel);
            }
        }
        
        // Try to look up a username to search by publisher.
        GameUser? publisher = this.GetUserByUsername(query, false); 
        if (publisher != null)
        {
            levels.AddRange(validLevels.Where(l => l.Publisher == publisher));
        }

        return new DatabaseList<GameLevel>(levels.OrderByDescending(l => l.Score), skip, count);
    }

    [Pure]
    public int GetTotalLevelCount(TokenGame game) => this.GameLevels.FilterByGameVersion(game).Count(l => l.StoryId == 0);
    
    [Pure]
    public int GetTotalLevelCount() => this.GameLevels.Count(l => l.StoryId == 0);

    [Pure]
    public int GetModdedLevelCount() => this.GameLevels.Count(l => l.StoryId == 0 && l.IsModded);

    public int GetTotalLevelsPublishedByUser(GameUser user)
        => this.GameLevels
            .Count(r => r.Publisher == user);
    
    public int GetTotalLevelsPublishedByUser(GameUser user, TokenGame game)
        => this.GameLevels
            .Count(r => r.Publisher == user && r._GameVersion == (int)game);
    
    [Pure]
    public int GetTotalTeamPickCount(TokenGame game) => this.GameLevels.FilterByGameVersion(game).Count(l => l.DateTeamPicked != null);

    [Pure]
    public GameLevel? GetLevelByIdAndType(string slotType, int id)
    {
        switch (slotType)
        {
            case "user":
                return this.GetLevelById(id);
            case "developer":
                if (id < 0)
                    return null;
                
                return this.GetStoryLevelById(id);
            default:
                return null;
        }
    }

    public GameLevel? GetLevelByRootResource(string rootResource) => this.GameLevelsIncluded
        .FirstOrDefault(level => level.RootResource == rootResource);
    
    [Pure]
    public GameLevel? GetLevelById(int id) => this.GameLevelsIncluded
        .FirstOrDefault(l => l.LevelId == id);

    public void AddTeamPickToLevel(GameLevel level)
    {
        this.Write(() =>
        {
            level.DateTeamPicked = this._time.Now;
        });
    }

    public void RemoveTeamPickFromLevel(GameLevel level)
    {
        this.Write(() =>
        {
            level.DateTeamPicked = null;
        });
    }

    public void SetLevelScores(Dictionary<GameLevel, float> scoresToSet)
    {
        this.Write(() =>
        {
            foreach ((GameLevel level, float score) in scoresToSet)
            {
                level.Score = score;
            }
        });
    }
    
    public void SetLevelModdedStatus(GameLevel level, bool modded)
    {
        this.Write(() =>
        {
            level.IsModded = modded;
        });
    }
    
    public void SetLevelModdedStatuses(Dictionary<GameLevel, bool> levels)
    {
        this.Write(() =>
        {
            foreach ((GameLevel? level, bool modded) in levels)
            {
                level.IsModded = modded;
            }
        });
    }

    public IEnumerable<GameSkillReward> GetSkillRewardsForLevel(GameLevel level)
    {
        return this.GameSkillRewards.Where(r => r.LevelId == level.LevelId);
    }

    public void UpdateSkillRewardsForLevel(GameLevel level, IEnumerable<GameSkillReward> rewards)
    {
        this.GameSkillRewards.RemoveRange(this.GetSkillRewardsForLevel(level));
        
        this.Write(() =>
        {
            foreach (GameSkillReward reward in rewards.Take(3))
            {
                GameSkillReward newReward = new()
                {
                    Id = reward.Id,
                    Title = reward.Title,
                    Enabled = reward.Enabled,
                    RequiredAmount = reward.RequiredAmount,
                    Level = level,
                    LevelId = level.LevelId,
                    ConditionType = reward.ConditionType,
                };
                
                this.GameSkillRewards.Add(newReward);
            }
        });
    }
}