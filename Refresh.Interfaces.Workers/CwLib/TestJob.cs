using Refresh.Database;
using Refresh.Database.Models.Authentication;
using Refresh.Database.Models.Levels;
using Refresh.Database.Query;
using Refresh.Workers;
using Refresh.Workers.State;

namespace Refresh.Interfaces.Workers.CwLib;

public class TestJob : CwLibJob
{
    public override Type JobStateType => typeof(AssetListState);
    
    public override void ExecuteJob(WorkContext context)
    {
        AssetListState state = new();

        DatabaseList<GameLevel> levels = context.Database.GetCoolLevels(100, 0, null, new LevelFilterSettings(TokenGame.Website));
        state.Assets.AddRange(levels.Items.Select(l => l.RootResource));

        this.JobState = state;
    }
}