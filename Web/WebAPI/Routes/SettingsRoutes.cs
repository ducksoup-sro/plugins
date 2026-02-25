using API.Database.Context;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatsonWebserver.Core;

namespace WebAPI.Routes;

public static class SettingsRoutes
{
    private static readonly string[] MaskedKeys = { "AuthRefreshSecret", "AuthAccessSecret", "password", "Password", "Secret" };

    public static async Task GetGlobalSettings(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            await using var context = new DuckSoup();
            var list = await context.GlobalSettings
                .Select(s => new { key = s.key, value = MaskValue(s.key, s.value) })
                .ToListAsync();
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(list));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task UpdateGlobalSetting(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = JsonConvert.DeserializeObject<GlobalSettingUpdateRequest>(body);
            var key = req?.key?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing key (body: { key, value })\"}");
                return;
            }
            await using var context = new DuckSoup();
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.key == key);
            if (setting == null)
            {
                context.GlobalSettings.Add(new API.Database.DuckSoup.GlobalSetting { key = key!, value = req?.value ?? "" });
            }
            else
            {
                setting.value = req?.value ?? "";
            }
            await context.SaveChangesAsync();
            AuditLog.Log("Settings.Update", key, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { status = "ok", key }));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    /// <summary>Read-only proxy/webserver-related settings from GlobalSettings.</summary>
    public static async Task GetProxySettings(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            await using var context = new DuckSoup();
            var keys = new[] { "WebserverHost", "WebserverPort", "AuthIssuer", "AuthAccessTokenExpiry", "AuthRefreshTokenExpiry" };
            var list = await context.GlobalSettings
                .Where(s => keys.Contains(s.key))
                .Select(s => new { key = s.key, value = s.value })
                .ToListAsync();
            var dict = list.ToDictionary(x => x.key, x => x.value);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(dict));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    private static string MaskValue(string key, string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        foreach (var m in MaskedKeys)
        {
            if (key?.IndexOf(m, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "********";
        }
        return value;
    }

    private class GlobalSettingUpdateRequest
    {
        public string key { get; set; } = "";
        public string value { get; set; } = "";
    }
}
