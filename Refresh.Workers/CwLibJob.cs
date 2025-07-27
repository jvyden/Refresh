using Refresh.Database.Models.Workers;
using Refresh.Workers.State;

namespace Refresh.Workers;

public abstract class CwLibJob : StartupJob, IJobStoresState
{
    public string JobId => this.GetType().Name;
    public object? JobState { get; set; } = null!;
    public abstract Type JobStateType { get; }
    public WorkerClass JobClass => WorkerClass.CwLib;
    public bool AutomaticallySetupState => false;

    public override bool CanExecute()
    {
        return JobState == null;
    }
}