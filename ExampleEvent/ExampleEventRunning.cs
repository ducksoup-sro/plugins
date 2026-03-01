using System.Threading.Tasks;
using API.Database;
using API.Event;
using Serilog;

namespace ExampleEvent;

public class ExampleEventRunning : IEventState
{
    private const string MessageKey = "Event.ExampleEvent.Message";
    private readonly IEvent _event;

    public ExampleEventRunning(IEvent iEvent) : base(iEvent)
    {
        _event = iEvent;
    }

    public override async Task Start()
    {
        var message = DatabaseHelper.GetSettingOrDefault(MessageKey, "");
        Log.Information("[ExampleEvent] Running phase – Message: {Message}", string.IsNullOrEmpty(message) ? "(none)" : message);
        await Task.Delay(3000);
        _event.SetEventState(EventStateEnum.Ending);
    }

    public override Task Stop()
    {
        Log.Information("[ExampleEvent] Running phase stopped");
        return Task.CompletedTask;
    }
}
