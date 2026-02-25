using System.Threading.Tasks;
using API.Database.DuckSoup;
using API.ServiceFactory;
using API.Services;
using Newtonsoft.Json;
using Serilog;
using WatsonWebserver.Core;
using WebApiPlugin;

namespace WebApiPlugin.Routes;

public static class AuthRoutes
{
    public static async Task InvalidateUser(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<InvalidateRequest>(body);
            var targetUsername = req?.Username?.Trim();
            if (string.IsNullOrEmpty(targetUsername))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Missing or invalid username\"}");
                return;
            }
            var userService = ServiceFactory.Load<IUserService>(typeof(IUserService));
            var user = userService.GetUser(targetUsername);
            if (user == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"User not found\"}");
                return;
            }
            var byUsername = WebApiHelpers.GetUsername(ctx) ?? "?";
            user.tokenVersion += 1;
            userService.AddUser(user);
            AuditLog.Log("Auth.Invalidate", $"target={targetUsername} by={byUsername}", byUsername);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\",\"message\":\"User invalidated (logged out)\"}");
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "[Auth.Invalidate] Error");
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    private class InvalidateRequest
    {
        public string? Username { get; set; }
    }
}
