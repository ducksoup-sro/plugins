using API.Database.Context;
using API.Database.DuckSoup;
using API.Event;
using API.ServiceFactory;
using Newtonsoft.Json;
using Quartz;
using WatsonWebserver.Core;
using WebAPI;

namespace WebAPI.Routes;

public static class EventRoutes
{
    private const string EventsDirectory = "events";

    /// <summary>Returns cron entries from DB grouped by event name.</summary>
    private static Dictionary<string, List<object>> GetCronsByEventName()
    {
        var cronsByEvent = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var context = new DuckSoup();
            foreach (var row in context.Events.ToList())
            {
                var name = row.Eventname ?? "";
                if (!cronsByEvent.ContainsKey(name))
                    cronsByEvent[name] = new List<object>();
                cronsByEvent[name].Add(new
                {
                    eventId = row.EventId,
                    eventname = row.Eventname,
                    crontime = row.Crontime,
                    comment = row.Comment
                });
            }
        }
        catch
        {
            // ignore DB errors (e.g. connection not configured)
        }
        return cronsByEvent;
    }

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

            var cronsByEvent = GetCronsByEventName();

            var loaded = new List<object>();
            foreach (var (_, evt) in eventManager.Loaders)
            {
                var name = evt.Name;
                cronsByEvent.TryGetValue(name, out var crons);
                loaded.Add(new
                {
                    name,
                    version = evt.Version,
                    author = evt.Author,
                    state = evt.GetCurrentState()?.ToString(),
                    crons = crons ?? new List<object>()
                });
            }

            var available = new List<object>();
            foreach (var folderName in eventManager.GetAvailableEventFolderNames())
            {
                cronsByEvent.TryGetValue(folderName, out var crons);
                available.Add(new
                {
                    name = folderName,
                    crons = crons ?? new List<object>()
                });
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
            var folderPath = eventManager.SearchEvent(eventsPath, name);
            if (string.IsNullOrEmpty(folderPath))
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Event not found (need folder with event.json in events directory)\"}");
                return;
            }

            var loader = eventManager.LoadEvent(folderPath);
            if (loader == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Failed to load event (invalid event.json or missing DLL)\"}");
                return;
            }

            var folderName = Path.GetFileName(folderPath);
            var evt = eventManager.StartEvent(loader, folderName);
            if (evt == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"Failed to start event\"}");
                return;
            }

            AuditLog.Log("Event.Load", evt.Name, WebApiHelpers.GetUsername(ctx));
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
            if (ok)
                AuditLog.Log("Event.Unload", name, WebApiHelpers.GetUsername(ctx));
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

    public static async Task AddCron(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<AddCronRequest>(body);
            var eventname = req?.eventname?.Trim();
            var crontime = req?.crontime?.Trim();
            if (string.IsNullOrEmpty(eventname) || string.IsNullOrEmpty(crontime))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing eventname or crontime (body: { \\\"eventname\\\": \\\"EventName\\\", \\\"crontime\\\": \\\"0 0 12 * * ?\\\", \\\"comment\\\": \\\"optional\\\" })\"}");
                return;
            }

            try
            {
                CronExpression.ValidateExpression(crontime);
            }
            catch (Exception cronEx)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send(JsonConvert.SerializeObject(new { error = "Invalid cron expression: " + (cronEx.Message ?? "invalid format") }));
                return;
            }

            using (var context = new DuckSoup())
            {
                context.Events.Add(new Event
                {
                    Eventname = eventname,
                    Crontime = crontime,
                    Comment = req?.comment?.Trim()
                });
                await context.SaveChangesAsync();
            }

            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventManager != null && eventManager.IsLoaded(eventname))
            {
                eventManager.ReloadEvent(eventname);
            }

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", eventname, crontime }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task DeleteCron(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var idStr = ctx.Request.Url.Parameters["id"]?.Trim();
            if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var eventId))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing or invalid id parameter (eventId)\"}");
                return;
            }

            string? eventname = null;
            using (var context = new DuckSoup())
            {
                var row = await context.Events.FindAsync(eventId);
                if (row == null)
                {
                    ctx.Response.StatusCode = 404;
                    await ctx.Response.Send("{\"error\":\"Cron entry not found\"}");
                    return;
                }
                eventname = row.Eventname;
                context.Events.Remove(row);
                await context.SaveChangesAsync();
            }

            var eventManager = ServiceFactory.Load<IEventManager>(typeof(IEventManager));
            if (eventname != null && eventManager != null && eventManager.IsLoaded(eventname))
            {
                eventManager.ReloadEvent(eventname);
            }

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", eventId }));
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

    private class AddCronRequest
    {
        public string? eventname { get; set; }
        public string? crontime { get; set; }
        public string? comment { get; set; }
    }
}
