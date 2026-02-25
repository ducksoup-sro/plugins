using System.Web;
using Newtonsoft.Json;
using WatsonWebserver.Core;

namespace WebAPI.Routes;

public static class AuditRoutes
{
    private static string? GetQueryParam(HttpContextBase ctx, string name)
    {
        var raw = ctx.Request.Url?.RawWithQuery;
        if (string.IsNullOrEmpty(raw)) return null;
        var queryStart = raw.IndexOf('?');
        if (queryStart < 0 || queryStart == raw.Length - 1) return null;
        var query = raw.Substring(queryStart + 1);
        return HttpUtility.ParseQueryString(query)?[name]?.Trim();
    }

    public static async Task GetAudit(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        var limitStr = GetQueryParam(ctx, "limit") ?? ctx.Request.Url?.Parameters?["limit"];
        int limit = 100;
        if (!string.IsNullOrEmpty(limitStr) && int.TryParse(limitStr, out var l) && l > 0)
            limit = System.Math.Min(l, 500);
        var actionPrefix = GetQueryParam(ctx, "actionPrefix") ?? ctx.Request.Url?.Parameters?["actionPrefix"]?.Trim();
        var list = await AuditLog.GetRecentAsync(limit, actionPrefix);
        ctx.Response.StatusCode = 200;
        await ctx.Response.Send(JsonConvert.SerializeObject(list));
    }
}
