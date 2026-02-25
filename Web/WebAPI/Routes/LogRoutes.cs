using System.Web;
using API.Logging;
using API.ServiceFactory;
using Newtonsoft.Json;
using WatsonWebserver.Core;

namespace WebAPI.Routes;

public static class LogRoutes
{
    /// <summary>Get query string parameters from the request URL (Watson static routes don't populate Url.Parameters).</summary>
    private static string? GetQueryParam(HttpContextBase ctx, string name)
    {
        var raw = ctx.Request.Url?.RawWithQuery;
        if (string.IsNullOrEmpty(raw))
            return null;
        var queryStart = raw.IndexOf('?');
        if (queryStart < 0 || queryStart == raw.Length - 1)
            return null;
        var query = raw.Substring(queryStart + 1);
        return HttpUtility.ParseQueryString(query)?[name]?.Trim();
    }

    public static async Task GetLogs(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx))
        {
            await ctx.Response.Send("{\"error\":\"Too many requests\"}");
            return;
        }
        try
        {
            if (!ServiceFactory.IsProvidedFor(typeof(ILogCapture)))
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.Send("{\"error\":\"Log capture not available\"}");
                return;
            }
            var capture = ServiceFactory.Load<ILogCapture>(typeof(ILogCapture));
            var limitStr = GetQueryParam(ctx, "limit") ?? ctx.Request.Url?.Parameters?["limit"]?.Trim();
            var minLevel = GetQueryParam(ctx, "minLevel") ?? ctx.Request.Url?.Parameters?["minLevel"]?.Trim();
            var source = GetQueryParam(ctx, "source") ?? ctx.Request.Url?.Parameters?["source"]?.Trim();
            var limit = 200;
            if (!string.IsNullOrEmpty(limitStr) && int.TryParse(limitStr, out var l) && l > 0 && l <= 2000)
                limit = l;
            var entries = capture.GetRecent(limit, string.IsNullOrEmpty(minLevel) ? null : minLevel, string.IsNullOrEmpty(source) ? null : source);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(entries));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }
}
