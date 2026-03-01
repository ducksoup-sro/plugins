using API;
using API.Command;
using API.Database;
using API.Plugin;
using API.Server;
using API.ServiceFactory;
using API.Webserver;
using Serilog;

namespace ExampleWebPlugin;

public class ExampleWebPlugin : IPlugin
{
    private IWebserverManager? _webserverManager;

    public string Name => "ExampleWebPlugin";
    public string Version => "1.0.0";
    public string Author => "DuckSoup";
    public ServerType ServerType => ServerType.None;

    private readonly List<IWebserverPluginRoute> routes = new List<IWebserverPluginRoute>
    {
        new WebserverPluginRoute
        {
            Title = "Example Dashboard",
            Path = "/example/dashboard",
            ShowInMenu = true,
            Parent = null,
            RequiredRole = API.Enums.UserRole.Authenticated
        },
        new WebserverPluginRoute
        {
            Title = "Example Data",
            Path = "/example/data",
            ShowInMenu = true,
            Parent = "/example/dashboard",
            RequiredRole = API.Enums.UserRole.Authenticated
        },
    };
    
    public void OnEnable()
    {
        Log.Information("ExamplePlugin v{Version} by {Author} is being enabled", Version, Author);
        
        _webserverManager = ServiceFactory.Load<IWebserverManager>(typeof(IWebserverManager));
        
        if (_webserverManager == null)
        {
            Log.Error("Failed to load IWebserverManager service");
            return;
        }

        RegisterPluginRoutes();
        ExampleWebPluginRoutes.Register(_webserverManager);
    }

    public void OnServerStart(IFakeServer server)
    {
        Log.Information("ExamplePlugin: Server started - {ServerType}", server.GetType().Name);
    }

    public List<Command> RegisterCommands()
    {
        return new List<Command>();
    }

    private void RegisterPluginRoutes()
    {
        if (_webserverManager == null) return;
        
        _webserverManager.RegisterPlugin(this, routes);
    }

    private const string MessageSettingKey = "Plugin.ExampleWebPlugin.Message";

    /// <summary>Re-read config when the dashboard triggers "Reload settings".</summary>
    public void InitSettings()
    {
        var message = DatabaseHelper.GetSettingOrDefault(MessageSettingKey, "Hello from ExampleWebPlugin");
        Log.Information("ExampleWebPlugin: InitSettings — Message = {Message}", message);
    }

    public void Dispose()
    {
        _webserverManager?.UnregisterPlugin(this);
    }
}