using API.Event;
using Serilog;

namespace ExampleEvent;

public class ExampleEventStarting : IEventState
{
    private readonly IEvent _event;

    public ExampleEventStarting(IEvent iEvent) : base(iEvent)
    {
        _event = iEvent;
    }

    public override async Task Start()
    {
        Log.Information("[ExampleEvent] Starting phase");
        await Task.Delay(20000);
        _event.SetEventState(EventStateEnum.Running);
    }

    public override Task Stop()
    {
        Log.Information("[ExampleEvent] Starting phase stopped");
        return Task.CompletedTask;
    }
}
