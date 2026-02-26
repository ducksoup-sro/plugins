using API.Event;

namespace ExampleEvent;

public class ExampleEvent : IEvent
{
    public override string Name => "ExampleEvent";
    public override string Version => "1.0.0";
    public override string Author => "DuckSoup";

    public override void OnEnable()
    {
        EventStates = new IEventState[Enum.GetNames(typeof(EventStateEnum)).Length];
        EventStates = new IEventState[Enum.GetNames(typeof(EventStateEnum)).Length];

        AddEventState(new ExampleEventStarting(this), EventStateEnum.Starting);
        AddEventState(new ExampleEventRunning(this), EventStateEnum.Running);
        AddEventState(new ExampleEventEnding(this), EventStateEnum.Ending);
    }

    public override void Dispose()
    {
        StopCurrentGameState();
        EventStates = null;
    }
}
