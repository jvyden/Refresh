using System.Reflection;
using Bunkum.Listener.Request;
using Bunkum.Core.Database;
using Bunkum.Core.Services;
using NotEnoughLogs;

namespace Refresh.GameServer.Services;

public class TimeProviderService : Service
{
    public TimeProvider TimeProvider { get; }

    internal TimeProviderService(Logger logger, TimeProvider timeProvider) : base(logger)
    {
        this.TimeProvider = timeProvider;
    }

    public override object? AddParameterToEndpoint(ListenerContext context, ParameterInfo paramInfo, Lazy<IDatabaseContext> database)
    {
        if (paramInfo.ParameterType == typeof(TimeProvider))
        {
            return this.TimeProvider;
        }

        return null;
    }
}