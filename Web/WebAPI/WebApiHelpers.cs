using API.Database.DuckSoup;
using API.Enums;
using WatsonWebserver.Core;

namespace WebAPI;

public static class WebApiHelpers
{
    public static string GetRateLimitKey(HttpContextBase ctx)
    {
        if (ctx.Metadata is User user)
            return "u:" + user.userId.ToString();
        var forwarded = ctx.Request.Headers?["X-Forwarded-For"];
        if (!string.IsNullOrEmpty(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return "ip:" + first;
        }
        return "ip:unknown";
    }

    public static bool RequireAdmin(HttpContextBase ctx)
    {
        return ctx.Metadata is User user && user.Role >= UserRole.Admin;
    }

    public static string? GetUsername(HttpContextBase ctx)
    {
        return (ctx.Metadata as User)?.username;
    }

    public static bool TryRateLimit(HttpContextBase ctx)
    {
        var key = GetRateLimitKey(ctx);
        if (!RateLimiter.TryAcquire(key))
        {
            ctx.Response.StatusCode = 429;
            ctx.Response.ContentType = "application/json";
            return false;
        }
        return true;
    }
}
