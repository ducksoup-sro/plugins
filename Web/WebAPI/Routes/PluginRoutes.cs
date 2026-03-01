using API.Database.DuckSoup;
using API.Enums;
using API.Plugin;
using API.ServiceFactory;
using API.Webserver;
using Newtonsoft.Json;
using WatsonWebserver.Core;

namespace WebAPI.Routes;

/// <summary>
/// Plugin list, load, unload (exposed by WebApiPlugin under /api/v1/plugins/).
/// </summary>
public static class PluginRoutes
{
    /// <summary>Returns plugins with their menu routes (grouped). Only plugins that have at least one ShowInMenu route are included. Routes are filtered by current user role.</summary>
    public static async Task GetPluginsWithRoutes(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            if (ctx.Metadata is not User user)
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.Send("{\"error\":\"Unauthorized\"}");
                return;
            }
            var webserverManager = ServiceFactory.Load<IWebserverManager>(typeof(IWebserverManager));
            if (webserverManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"WebserverManager not available\"}");
                return;
            }
            var allPluginRoutes = webserverManager.GetRegisteredPlugins();
            var plugins = new List<object>();
            foreach (var (plugin, routes) in allPluginRoutes)
            {
                var accessibleRoutes = new List<object>();
                foreach (var route in routes)
                {
                    if (!route.ShowInMenu) continue;
                    var hasAccess = route.RequiredRole == UserRole.Anyone || route.RequiredRole == UserRole.Anonymous
                        || (route.RequiredRole == UserRole.Authenticated && user != null)
                        || (user != null && user.Role >= route.RequiredRole);
                    if (!hasAccess) continue;
                    accessibleRoutes.Add(new
                    {
                        title = route.Title,
                        path = route.Path
                    });
                }
                if (accessibleRoutes.Count > 0)
                    plugins.Add(new { name = plugin.Name, routes = accessibleRoutes });
            }
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { plugins }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task ListPlugins(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var pm = ServiceFactory.Load<IPluginManager>(typeof(IPluginManager));
            if (pm == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"PluginManager not available\"}");
                return;
            }

            var infos = pm.GetLoadedPluginInfos();
            var loadedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loaded = new List<object>();
            foreach (var info in infos)
            {
                if (!string.IsNullOrEmpty(info.Folder))
                    loadedFolderNames.Add(info.Folder);
                loaded.Add(new
                {
                    name = info.Name,
                    version = info.Version,
                    author = info.Author,
                    folder = info.Folder
                });
            }

            var pluginsDir = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
            var available = new List<object>();
            if (Directory.Exists(pluginsDir))
            {
                foreach (var dir in Directory.GetDirectories(pluginsDir))
                {
                    var folderName = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(folderName)) continue;
                    var configPath = Path.Combine(dir, "plugin.json");
                    if (!File.Exists(configPath)) continue;
                    var alreadyLoaded = loadedFolderNames.Contains(folderName);
                    available.Add(new { folderName, alreadyLoaded });
                }
            }

            var result = new { loaded, available };
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(result));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task LoadPlugin(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<PluginNameRequest>(body);
            var name = req?.name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name (body: { \\\"name\\\": \\\"PluginName\\\" })\"}");
                return;
            }

            var pm = ServiceFactory.Load<IPluginManager>(typeof(IPluginManager));
            if (pm == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"PluginManager not available\"}");
                return;
            }
            var pluginsDir = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
            var folder = Path.Combine(pluginsDir, name);

            var loadedInfos = pm.GetLoadedPluginInfos();
            var loadedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in loadedInfos)
            {
                if (!string.IsNullOrEmpty(info.Folder))
                    loadedFolderNames.Add(info.Folder);
            }
            if (loadedFolderNames.Contains(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send(JsonConvert.SerializeObject(new { error = "Plugin from this folder is already loaded" }));
                return;
            }
            if (pm.IsLoaded(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Plugin already loaded\"}");
                return;
            }
            if (!Directory.Exists(folder))
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Plugin folder not found in plugins directory\"}");
                return;
            }

            var loader = pm.LoadPlugin(folder);
            if (loader == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Failed to load plugin\"}");
                return;
            }

            var plugin = pm.StartPlugin(loader, name);
            if (plugin == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Failed to start plugin\"}");
                return;
            }

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", name = plugin.Name, version = plugin.Version }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task UnloadPlugin(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<PluginNameRequest>(body);
            var name = req?.name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name (body: { \\\"name\\\": \\\"PluginName\\\" })\"}");
                return;
            }

            var pm = ServiceFactory.Load<IPluginManager>(typeof(IPluginManager));
            if (pm == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"PluginManager not available\"}");
                return;
            }
            if (!pm.IsLoaded(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Plugin not loaded\"}");
                return;
            }

            var ok = pm.UnloadPlugin(name);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = ok ? "ok" : "failed", name }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    /// <summary>Calls InitSettings() on the plugin if it implements IHasInitSettings. Admin only.</summary>
    public static async Task InitSettings(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<PluginNameRequest>(body);
            var name = req?.name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name (body: { \\\"name\\\": \\\"PluginName\\\" })\"}");
                return;
            }
            var pm = ServiceFactory.Load<IPluginManager>(typeof(IPluginManager));
            if (pm == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"PluginManager not available\"}");
                return;
            }
            var ok = pm.TriggerInitSettings(name);
            if (!ok)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send(JsonConvert.SerializeObject(new { error = "Plugin not found or InitSettings failed" }));
                return;
            }
            AuditLog.Log("Plugin.InitSettings", name, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", name }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    private class PluginNameRequest
    {
        public string? name { get; set; }
    }
}
