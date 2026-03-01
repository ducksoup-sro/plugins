using System;
using System.Threading.Tasks;
using API.Database.Context;
using API.Database.DuckSoup;
using API.Webserver;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace ExampleEvent;

/// <summary>
/// HTTP route handlers for ExampleEvent – registered by the event, not by the WebAPI.
/// Shows how an event serves its own settings and data.
/// </summary>
public static class ExampleEventRoutes
{
    public const string EventName = "ExampleEvent";
    private const string DefaultCron = "0 0 * * * ?"; // every hour
    private static string MessageKey => $"Event.{EventName}.Message";
    private const string ApiPrefix = "/api/v1/web/events/ExampleEvent";

    /// <summary>Event has storage responsibility: ensure a cron entry exists so the event can be scheduled.</summary>
    public static void EnsureDefaultCronExists()
    {
        try
        {
            using var context = new DuckSoup();
            var exists = context.Events.Any(e => e.Eventname == EventName);
            if (!exists)
            {
                context.Events.Add(new Event
                {
                    Eventname = EventName,
                    Crontime = DefaultCron,
                    Comment = "Example event – every hour (added by plugin)"
                });
                context.SaveChanges();
            }
        }
        catch
        {
            // DB may not be configured; ignore
        }
    }

    public static void Register(IWebserverManager manager)
    {
        if (manager == null) return;
        manager.addStaticRoute(HttpMethod.GET, ApiPrefix + "/settings", GetSettings);
        manager.addStaticRoute(HttpMethod.PATCH, ApiPrefix + "/settings", UpdateSettings);
        manager.addStaticRoute(HttpMethod.GET, ApiPrefix + "/data", GetData);
    }

    public static async Task GetSettings(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        try
        {
            await using var context = new DuckSoup();
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.key == MessageKey);
            var message = setting?.value ?? "";
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { message }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task UpdateSettings(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrEmpty(body) ? null : JsonConvert.DeserializeObject<SettingsRequest>(body);
            var message = req?.message?.Trim() ?? "";
            await using var context = new DuckSoup();
            var setting = await context.GlobalSettings.AsTracking().FirstOrDefaultAsync(s => s.key == MessageKey);
            if (setting == null)
                context.GlobalSettings.Add(new GlobalSetting { key = MessageKey, value = message });
            else
                setting.value = message;
            await context.SaveChangesAsync();
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", message }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    /// <summary>Returns current message (e.g. for display when event is running).</summary>
    public static async Task GetData(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        try
        {
            await using var context = new DuckSoup();
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.key == MessageKey);
            var message = setting?.value ?? "";
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { message, eventName = EventName }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    private class SettingsRequest
    {
        public string? message { get; set; }
    }
}
