using API.Event;
using API.ServiceFactory;
using API.Webserver;

namespace ExampleEvent;

public class ExampleEvent : IEvent
{
    public override string Name => "ExampleEvent";
    public override string Version => "1.0.0";
    public override string Author => "DuckSoup";

    public override IReadOnlyList<IWebserverPluginRoute> GetMenuRoutes() => new[]
    {
        new WebserverPluginRoute
        {
            Title = "Settings",
            Path = "example-event/settings",
            ShowInMenu = true,
            RequiredRole = API.Enums.UserRole.Authenticated
        }
    };

    public override void OnEnable()
    {
        EventStates = new IEventState[Enum.GetNames(typeof(EventStateEnum)).Length];

        AddEventState(new ExampleEventStarting(this), EventStateEnum.Starting);
        AddEventState(new ExampleEventRunning(this), EventStateEnum.Running);
        AddEventState(new ExampleEventEnding(this), EventStateEnum.Ending);

        ExampleEventRoutes.EnsureDefaultCronExists();

        var webserverManager = ServiceFactory.Load<IWebserverManager>(typeof(IWebserverManager));
        ExampleEventRoutes.Register(webserverManager);
    }

    public override void Dispose()
    {
        StopCurrentGameState();
        EventStates = null;
    }
}
