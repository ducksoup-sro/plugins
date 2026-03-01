using System;
using System.Threading.Tasks;
using API.Database.Context;
using API.Database.DuckSoup;
using API.Webserver;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace ExampleWebPlugin;

/// <summary>
/// HTTP route handlers for ExampleWebPlugin – registered by the plugin, not by the WebAPI.
/// Shows how a plugin serves its own settings and example data.
/// </summary>
public static class ExampleWebPluginRoutes
{
    private const string MessageSettingKey = "Plugin.ExampleWebPlugin.Message";
    private const string ApiPrefix = "/api/v1/web/plugins/example-web-plugin";

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
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.key == MessageSettingKey);
            var message = setting?.value ?? "Hello from ExampleWebPlugin";
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
            var setting = await context.GlobalSettings.AsTracking().FirstOrDefaultAsync(s => s.key == MessageSettingKey);
            if (setting == null)
                context.GlobalSettings.Add(new GlobalSetting { key = MessageSettingKey, value = message });
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

    /// <summary>Example data endpoint – returns settings plus sample structure for plugin authors.</summary>
    public static async Task GetData(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        try
        {
            await using var context = new DuckSoup();
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.key == MessageSettingKey);
            var message = setting?.value ?? "Hello from ExampleWebPlugin";

            var sampleData = new
            {
                message,
                description = "Example data from ExampleWebPlugin – settings plus sample structure.",
                sampleItems = new[]
                {
                    new { id = 1, label = "Item A", value = "Value A" },
                    new { id = 2, label = "Item B", value = "Value B" }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(sampleData));
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
