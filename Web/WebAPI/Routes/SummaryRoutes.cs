using System.Threading.Tasks;
using API;
using API.Database.Context;
using API.ServiceFactory;
using API.Session;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using WebApiPlugin;

namespace WebApiPlugin.Routes;

public static class SummaryRoutes
{
    public static async Task GetSummary(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx))
        {
            await ctx.Response.Send("{\"error\":\"Too many requests\"}");
            return;
        }

        try
        {
            int servicesTotal = 0;
            int servicesRunning = 0;
            int sessionsActive = 0;
            int machinesCount = 0;

            var serverManager = ServiceFactory.Load<API.Server.IServerManager>(typeof(API.Server.IServerManager));
            if (serverManager != null)
                servicesRunning = serverManager.Servers?.Count ?? 0;

            var shared = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
            if (shared != null)
            {
                sessionsActive = (shared.DownloadSessions?.Count ?? 0)
                    + (shared.GatewaySessions?.Count ?? 0)
                    + (shared.AgentSessions?.Count ?? 0);
            }

            await using (var context = new DuckSoup())
            {
                servicesTotal = await context.Services.CountAsync();
                machinesCount = await context.Machines.CountAsync();
            }

            var summary = new
            {
                servicesTotal,
                servicesRunning,
                sessionsActive,
                machinesCount
            };

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(summary));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }
}
