using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Event;
using API.ServiceFactory;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using WebApiPlugin;

namespace WebApiPlugin.Routes;

public static class EventRoutes
{
    private const string EventsDirectory = "events";

    public static async Task ListEvents(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"EventManager not available\"}");
                return;
            }

            var loaded = new List<object>();
            foreach (var (_, evt) in eventManager.Loaders)
            {
                loaded.Add(new
                {
                    name = evt.Name,
                    version = evt.Version,
                    author = evt.Author,
                    state = evt.GetCurrentState()?.ToString()
                });
            }

            var available = new List<string>();
            var eventsPath = Path.Combine(Directory.GetCurrentDirectory(), EventsDirectory);
            if (Directory.Exists(eventsPath))
            {
                foreach (var file in Directory.GetFiles(eventsPath))
                {
                    if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name))
                        available.Add(name);
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

    public static async Task LoadEvent(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<EventNameRequest>(body);
            var name = req?.name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name (body: { \\\"name\\\": \\\"EventName\\\" })\"}");
                return;
            }

            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"EventManager not available\"}");
                return;
            }
            if (eventManager.IsLoaded(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Event already loaded\"}");
                return;
            }

            var eventsPath = Path.Combine(Directory.GetCurrentDirectory(), EventsDirectory);
            var file = eventManager.SearchEvent(eventsPath, name);
            if (string.IsNullOrEmpty(file))
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Event not found in events directory\"}");
                return;
            }

            var loader = eventManager.LoadEvent(file);
            var evt = eventManager.StartEvent(loader);
            if (evt == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Failed to start event\"}");
                return;
            }

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", name = evt.Name, version = evt.Version }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task UnloadEvent(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var name = ctx.Request.Url.Parameters["name"]?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
                return;
            }

            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"EventManager not available\"}");
                return;
            }
            if (!eventManager.IsLoaded(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Event not loaded\"}");
                return;
            }

            var ok = eventManager.UnloadEvent(name);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = ok ? "ok" : "failed", name }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task GetEventState(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var name = ctx.Request.Url.Parameters["name"]?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
                return;
            }

            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"EventManager not available\"}");
                return;
            }

            var evt = eventManager.Loaders.Values.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (evt == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Event not found\"}");
                return;
            }

            var state = evt.GetCurrentState();
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { name = evt.Name, state = state?.ToString(), stateId = (int?)state }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task SetEventState(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var name = ctx.Request.Url.Parameters["name"]?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
                return;
            }

            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<EventStateRequest>(body);
            EventStateEnum? stateEnum = null;
            if (req?.state != null)
            {
                var v = req.state;
                if (v is long or int)
                {
                    var i = Convert.ToInt32(v);
                    if (i >= 0 && i <= 2)
                        stateEnum = (EventStateEnum)i;
                }
                else if (v is string s && Enum.TryParse<EventStateEnum>(s, true, out var p))
                    stateEnum = p;
            }
            if (!stateEnum.HasValue)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send(
                    string.IsNullOrEmpty(req?.state?.ToString())
                        ? "{\"error\":\"Missing state (body: { \\\"state\\\": 0|1|2 or \\\"Starting\\\"|\\\"Running\\\"|\\\"Ending\\\" })\"}"
                        : "{\"error\":\"Invalid state (use 0=Starting, 1=Running, 2=Ending or state name)\"}");
                return;
            }

            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"EventManager not available\"}");
                return;
            }

            var evt = eventManager.Loaders.Values.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (evt == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Event not found\"}");
                return;
            }

            evt.SetEventState(stateEnum.Value);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", name = evt.Name, state = stateEnum.Value.ToString() }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    private class EventNameRequest
    {
        public string? name { get; set; }
    }

    private class EventStateRequest
    {
        public object? state { get; set; }
    }
}
