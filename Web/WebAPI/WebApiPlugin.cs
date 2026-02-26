using API.Command;
using API.Plugin;
using API.Server;
using API.ServiceFactory;
using API.Webserver;
using WebAPI.Database;
using WebAPI.Routes;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace WebAPI;

public class WebApiPlugin : IPlugin
{
    private IWebserverManager? _webserverManager;
    private readonly List<IWebserverPluginRoute> _routes = new();

    public string Name => "WebApi";
    public string Version => "1.0.0";
    public string Author => "DuckSoup";
    public API.ServerType ServerType => API.ServerType.None;

    public void OnEnable()
    {
        PacketLogStore.Subscribe();
        _webserverManager = ServiceFactory.Load<IWebserverManager>(typeof(IWebserverManager));
        if (_webserverManager == null) return;

        WebApiDatabaseInit.InitDatabaseAsync(_webserverManager).GetAwaiter().GetResult();

        RegisterRoutes();
        RegisterProtectedPrefix();
        RegisterPluginMenu();
    }

    public void OnServerStart(IFakeServer server) { }

    public List<Command> RegisterCommands() => new List<Command>();

    private void RegisterRoutes()
    {
        // Services
        _webserverManager!.addStaticRoute(HttpMethod.GET, "/api/v1/web/services", ServiceRoutes.ListServices);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/services", ServiceRoutes.AddService);
        _webserverManager.addParameterRoute(HttpMethod.PATCH, "/api/v1/web/services/{name}", ServiceRoutes.UpdateService);
        _webserverManager.addParameterRoute(HttpMethod.DELETE, "/api/v1/web/services/{name}", ServiceRoutes.RemoveService);
        _webserverManager.addParameterRoute(HttpMethod.POST, "/api/v1/web/services/{name}/start", ServiceRoutes.StartService);
        _webserverManager.addParameterRoute(HttpMethod.POST, "/api/v1/web/services/{name}/stop", ServiceRoutes.StopService);

        // Sessions
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/sessions", SessionRoutes.ListSessions);
        _webserverManager.addParameterRoute(HttpMethod.GET, "/api/v1/web/sessions/{guid}", SessionRoutes.GetSessionDetail);
        _webserverManager.addParameterRoute(HttpMethod.GET, "/api/v1/web/sessions/{guid}/data", SessionRoutes.GetSessionData);
        _webserverManager.addParameterRoute(HttpMethod.POST, "/api/v1/web/sessions/{guid}/disconnect", SessionRoutes.DisconnectSession);
        _webserverManager.addParameterRoute(HttpMethod.GET, "/api/v1/web/sessions/{guid}/packets", SessionRoutes.GetSessionPackets);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/sessions/packet", SessionRoutes.SendPacket);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/sessions/broadcast", SessionRoutes.BroadcastPacket);

        // Machines
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/machines", MachineRoutes.ListMachines);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/machines", MachineRoutes.AddMachine);
        _webserverManager.addParameterRoute(HttpMethod.PATCH, "/api/v1/web/machines/{id}", MachineRoutes.UpdateMachine);
        _webserverManager.addParameterRoute(HttpMethod.DELETE, "/api/v1/web/machines/{id}", MachineRoutes.DeleteMachine);

        // Audit
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/audit", AuditRoutes.GetAudit);

        // Events (list, load, unload, get/set state, crons)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/events", EventRoutes.ListEvents);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/events/load", EventRoutes.LoadEvent);
        _webserverManager.addParameterRoute(HttpMethod.POST, "/api/v1/web/events/{name}/unload", EventRoutes.UnloadEvent);
        _webserverManager.addParameterRoute(HttpMethod.GET, "/api/v1/web/events/{name}/state", EventRoutes.GetEventState);
        _webserverManager.addParameterRoute(HttpMethod.PATCH, "/api/v1/web/events/{name}/state", EventRoutes.SetEventState);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/events/crons", EventRoutes.AddCron);
        _webserverManager.addParameterRoute(HttpMethod.DELETE, "/api/v1/web/events/crons/{id}", EventRoutes.DeleteCron);

        // Dashboard summary
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/summary", SummaryRoutes.GetSummary);

        // System metrics (proxy CPU / RAM)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/system/metrics", SystemRoutes.GetMetrics);
        // System info (database provider, data source, database names — no secrets)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/system/info", SystemRoutes.GetSystemInfo);

        // Serilog log (recent in-memory entries)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/logs", LogRoutes.GetLogs);

        // CORS (list, add, remove)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/cors/origins", CorsRoutes.ListOrigins);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/cors/origins", CorsRoutes.AddOrigin);
        _webserverManager.addStaticRoute(HttpMethod.DELETE, "/api/v1/web/cors/origins", CorsRoutes.RemoveOrigin);

        // Settings (global + proxy read-only)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/settings/global", SettingsRoutes.GetGlobalSettings);
        _webserverManager.addStaticRoute(HttpMethod.PATCH, "/api/v1/web/settings/global", SettingsRoutes.UpdateGlobalSetting);
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/web/settings/proxy", SettingsRoutes.GetProxySettings);

        // Auth (admin invalidate user from panel)
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/web/auth/invalidate", AuthRoutes.InvalidateUser);

        // Plugins (list, load, unload)
        _webserverManager.addStaticRoute(HttpMethod.GET, "/api/v1/plugins/list", PluginRoutes.ListPlugins);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/plugins/load", PluginRoutes.LoadPlugin);
        _webserverManager.addStaticRoute(HttpMethod.POST, "/api/v1/plugins/unload", PluginRoutes.UnloadPlugin);
    }

    private void RegisterProtectedPrefix()
    {
        _webserverManager!.addProtectedPrefix("/api/v1/web/", new[] { API.Enums.UserRole.Authenticated });
        _webserverManager.addProtectedPrefix("/api/v1/plugins/list", new[] { API.Enums.UserRole.Authenticated });
        _webserverManager.addProtectedPrefix("/api/v1/plugins/load", new[] { API.Enums.UserRole.Admin });
        _webserverManager.addProtectedPrefix("/api/v1/plugins/unload", new[] { API.Enums.UserRole.Admin });
        _webserverManager.addProtectedPrefix("/api/v1/web/auth/invalidate", new[] { API.Enums.UserRole.Admin });
        _webserverManager.addProtectedPrefix("/api/v1/web/sessions/broadcast", new[] { API.Enums.UserRole.Admin });
    }

    private void RegisterPluginMenu()
    {
        _routes.Add(new WebserverPluginRoute
        {
            Title = "Web API",
            Path = "/api/v1/web",
            ShowInMenu = true,
            RequiredRole = API.Enums.UserRole.Authenticated
        });
        _routes.Add(new WebserverPluginRoute
        {
            Title = "Events",
            Path = "/dashboard/events",
            ShowInMenu = true,
            RequiredRole = API.Enums.UserRole.Authenticated
        });
        _webserverManager!.RegisterPlugin(this, _routes);
    }

    public void Dispose() { }
}
