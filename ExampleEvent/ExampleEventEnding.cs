using API.Event;
using Serilog;

namespace ExampleEvent;

public class ExampleEventEnding : IEventState
{
    public ExampleEventEnding(IEvent iEvent) : base(iEvent)
    {
    }

    public override async Task Start()
    {
        Log.Information("[ExampleEvent] Ending phase");
        await Task.Delay(10000);
    }

    public override Task Stop()
    {
        Log.Information("[ExampleEvent] Ending phase stopped");
        return Task.CompletedTask;
    }
}
