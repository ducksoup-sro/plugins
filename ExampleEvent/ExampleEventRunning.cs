using API.Event;
using Serilog;

namespace ExampleEvent;

public class ExampleEventRunning : IEventState
{
    private readonly IEvent _event;

    public ExampleEventRunning(IEvent iEvent) : base(iEvent)
    {
        _event = iEvent;
    }

    public override async Task Start()
    {
        Log.Information("[ExampleEvent] Running phase");
        await Task.Delay(30000);
        _event.SetEventState(EventStateEnum.Ending);
    }

    public override Task Stop()
    {
        Log.Information("[ExampleEvent] Running phase stopped");
        return Task.CompletedTask;
    }
}
