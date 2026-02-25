using API.Database.Context;
using API.Database.DuckSoup;
using API.ServiceFactory;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using WebAPI.Dto;

namespace WebAPI.Routes;

public static class CorsRoutes
{
    public static async Task ListOrigins(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var webserverManager = ServiceFactory.Load<API.Webserver.IWebserverManager>(typeof(API.Webserver.IWebserverManager));
            if (webserverManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"WebserverManager not available\"}");
                return;
            }
            var all = webserverManager.GetAllowedOrigins();
            var pluginList = webserverManager.GetPluginAllowedOrigins();
            var pluginSet = new HashSet<string>(pluginList, StringComparer.OrdinalIgnoreCase);
            var defaultList = all.Where(o => !pluginSet.Contains(o)).ToList();
            var response = new CorsOriginsResponse
            {
                Default = defaultList,
                Plugin = new List<string>(pluginList)
            };
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(response));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task AddOrigin(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = JsonConvert.DeserializeObject<CorsOriginRequest>(body);
            var origin = req?.Origin?.Trim();
            if (string.IsNullOrEmpty(origin))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing or empty origin\"}");
                return;
            }
            var webserverManager = ServiceFactory.Load<API.Webserver.IWebserverManager>(typeof(API.Webserver.IWebserverManager));
            if (webserverManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"WebserverManager not available\"}");
                return;
            }
            await using (var db = new DuckSoup())
            {
                if (await db.CorsOrigins.AnyAsync(o => o.Origin == origin))
                {
                    ctx.Response.StatusCode = 409;
                    await ctx.Response.Send(JsonConvert.SerializeObject(new { error = "Origin already exists" }));
                    return;
                }
                db.CorsOrigins.Add(new CorsOrigin { Origin = origin });
                await db.SaveChangesAsync();
            }
            webserverManager.AddAllowedOrigin(origin);
            AuditLog.Log("Cors.AddOrigin", origin, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { origin }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task RemoveOrigin(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var origin = ctx.Request.Url.Parameters["origin"];
        if (string.IsNullOrWhiteSpace(origin))
        {
            try { origin = JsonConvert.DeserializeObject<CorsOriginRequest>(ctx.Request.DataAsString ?? "{}")?.Origin; } catch { }
        }
        if (string.IsNullOrWhiteSpace(origin))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing origin (query param ?origin=... or body { \\\"origin\\\": \\\"...\\\" })\"}");
            return;
        }
        origin = origin.Trim();
        try
        {
            var webserverManager = ServiceFactory.Load<API.Webserver.IWebserverManager>(typeof(API.Webserver.IWebserverManager));
            if (webserverManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"WebserverManager not available\"}");
                return;
            }
            await using (var db = new DuckSoup())
            {
                var entity = await db.CorsOrigins.FirstOrDefaultAsync(o => o.Origin == origin);
                if (entity != null)
                {
                    db.CorsOrigins.Remove(entity);
                    await db.SaveChangesAsync();
                }
            }
            webserverManager.RemoveAllowedOrigin(origin);
            AuditLog.Log("Cors.RemoveOrigin", origin, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { removed = origin }));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }
}
