using API.Database.Context;
using API.Database.DuckSoup;
using API.Server;
using API.ServiceFactory;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SilkroadSecurityAPI;
using WatsonWebserver.Core;
using WebAPI.Dto;

namespace WebAPI.Routes;

public static class ServiceRoutes
{
    public static async Task ListServices(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
            if (serverManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ServerManager not available\"}");
                return;
            }

            using var context = new DuckSoup();
            var services = await context.Services
                .Include(s => s.LocalMachine_Machine)
                .Include(s => s.RemoteMachine_Machine)
                .Include(s => s.SpoofMachine_Machine)
                .ToListAsync();

            var startedIds = new HashSet<int>(serverManager.Servers.Select(s => s.Service.ServiceId));
            var dtos = services.Select(s => new ServiceDto
            {
                ServiceId = s.ServiceId,
                Name = s.Name,
                ServerType = s.ServerType.ToString(),
                SecurityType = ((SecurityType)s.SecurityType).ToString(),
                RemotePort = s.RemotePort,
                BindPort = s.BindPort,
                AutoStart = s.AutoStart,
                Started = startedIds.Contains(s.ServiceId),
                LocalMachineId = s.LocalMachine_MachineId,
                RemoteMachineId = s.RemoteMachine_MachineId,
                LocalAddress = s.LocalMachine_Machine?.Address,
                RemoteAddress = s.RemoteMachine_Machine?.Address,
                SpoofMachineId = s.SpoofMachine_MachineId,
                SpoofAddress = s.SpoofMachine_Machine?.Address
            }).ToList();

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(dtos));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task AddService(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = JsonConvert.DeserializeObject<AddServiceRequest>(body);
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid body or missing name\"}");
                return;
            }

            using (var context = new DuckSoup())
            {
                var exists = await context.Services.AnyAsync(s => s.Name == req.Name);
                if (exists)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("{\"error\":\"Service with this name already exists\"}");
                    return;
                }

                var localMachine = await context.Machines.FindAsync(req.LocalMachineId);
                var remoteMachine = await context.Machines.FindAsync(req.RemoteMachineId);
                if (localMachine == null || remoteMachine == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("{\"error\":\"LocalMachine or RemoteMachine not found\"}");
                    return;
                }

                if (!ServiceValidation.IsValidServerType(req.ServerType))
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("{\"error\":\"Invalid serverType (use DownloadServer=1, GatewayServer=2, AgentServer=3)\"}");
                    return;
                }
                if (!ServiceValidation.IsValidSecurityType(req.SecurityType))
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("{\"error\":\"Invalid securityType (use VSRO188=0, ISRO_R=1)\"}");
                    return;
                }
                var portError = await ServiceValidation.ValidatePortsAsync(req.LocalMachineId, req.BindPort, req.RemotePort);
                if (portError != null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send(JsonConvert.SerializeObject(new { error = portError }));
                    return;
                }

                var service = new Service
                {
                    Name = req.Name.Trim(),
                    ServerType = (API.ServerType)req.ServerType,
                    SecurityType = (SecurityType)req.SecurityType,
                    RemotePort = req.RemotePort,
                    BindPort = req.BindPort,
                    ByteLimitation = req.ByteLimitation,
                    AutoStart = req.AutoStart,
                    LocalMachine_MachineId = req.LocalMachineId,
                    RemoteMachine_MachineId = req.RemoteMachineId,
                    SpoofMachine_MachineId = req.SpoofMachineId
                };
                context.Services.Add(service);
                await context.SaveChangesAsync();

                // Reload with navigation properties for AddServer
                var added = await context.Services
                    .Include(s => s.LocalMachine_Machine)
                    .Include(s => s.RemoteMachine_Machine)
                    .Include(s => s.SpoofMachine_Machine)
                    .FirstAsync(s => s.ServiceId == service.ServiceId);

                var serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
                var result = serverManager!.AddServer(added);
                if (result.IsFaulted)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.Send(JsonConvert.SerializeObject(new { error = result.ToString() }));
                    return;
                }

                if (req.AutoStart)
                    serverManager.Start(added.Name);
            }
            AuditLog.Log("Service.Add", req.Name?.Trim(), WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 201;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task RemoveService(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var name = ctx.Request.Url.Parameters["name"];
        if (string.IsNullOrEmpty(name))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
            return;
        }

        try
        {
            var serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
            serverManager?.Stop(name);

            using var context = new DuckSoup();
            var service = await context.Services.FirstOrDefaultAsync(s => s.Name == name);
            if (service != null)
            {
                context.Services.Remove(service);
                await context.SaveChangesAsync();
            }
            AuditLog.Log("Service.Remove", name, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task StartService(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var name = ctx.Request.Url.Parameters["name"];
        if (string.IsNullOrEmpty(name))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
            return;
        }

        try
        {
            using var context = new DuckSoup();
            var service = await context.Services
                .Include(s => s.LocalMachine_Machine)
                .Include(s => s.RemoteMachine_Machine)
                .Include(s => s.SpoofMachine_Machine)
                .FirstOrDefaultAsync(s => s.Name == name);
            if (service == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Service not found\"}");
                return;
            }

            var serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
            if (serverManager == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ServerManager not available\"}");
                return;
            }

            var alreadyRunning = serverManager.Servers.Any(s => s.Service.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!alreadyRunning)
            {
                var addResult = serverManager.AddServer(service);
                if (addResult.IsFaulted)
                {
                    ctx.Response.StatusCode = 500;
                    await ctx.Response.Send(JsonConvert.SerializeObject(new { error = addResult.ToString() }));
                    return;
                }
            }

            serverManager.Start(name);
            AuditLog.Log("Service.Start", name, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task StopService(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var name = ctx.Request.Url.Parameters["name"];
        if (string.IsNullOrEmpty(name))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
            return;
        }

        try
        {
            var serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
            serverManager?.Stop(name);
            AuditLog.Log("Service.Stop", name, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task UpdateService(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var name = ctx.Request.Url.Parameters["name"];
        if (string.IsNullOrEmpty(name))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing name parameter\"}");
            return;
        }

        try
        {
            var body = ctx.Request.DataAsString;
            var req = JsonConvert.DeserializeObject<UpdateServiceRequest>(body);
            if (req == null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid body\"}");
                return;
            }

            using var context = new DuckSoup();
            var service = await context.Services
                .Include(s => s.LocalMachine_Machine)
                .Include(s => s.RemoteMachine_Machine)
                .Include(s => s.SpoofMachine_Machine)
                .FirstOrDefaultAsync(s => s.Name == name);
            if (service == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Service not found\"}");
                return;
            }

            var localMachineId = req.LocalMachineId ?? service.LocalMachine_MachineId;
            var bindPort = req.BindPort ?? service.BindPort;
            var remotePort = req.RemotePort ?? service.RemotePort;
            var portError = await ServiceValidation.ValidatePortsAsync(localMachineId, bindPort, remotePort, service.ServiceId);
            if (portError != null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send(JsonConvert.SerializeObject(new { error = portError }));
                return;
            }

            if (req.RemotePort.HasValue) service.RemotePort = req.RemotePort.Value;
            if (req.BindPort.HasValue) service.BindPort = req.BindPort.Value;
            if (req.ByteLimitation.HasValue) service.ByteLimitation = req.ByteLimitation.Value;
            if (req.AutoStart.HasValue) service.AutoStart = req.AutoStart.Value;
            if (req.LocalMachineId.HasValue) service.LocalMachine_MachineId = req.LocalMachineId.Value;
            if (req.RemoteMachineId.HasValue) service.RemoteMachine_MachineId = req.RemoteMachineId.Value;
            if (req.ClearSpoof)
                service.SpoofMachine_MachineId = null;
            else if (req.SpoofMachineId.HasValue)
                service.SpoofMachine_MachineId = req.SpoofMachineId.Value;
            if (!string.IsNullOrWhiteSpace(req.Name) && req.Name != name)
            {
                var exists = await context.Services.AnyAsync(s => s.Name == req.Name.Trim());
                if (exists)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("{\"error\":\"Service name already exists\"}");
                    return;
                }
                service.Name = req.Name.Trim();
            }

            await context.SaveChangesAsync();

            if (req.Restart)
            {
                var serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
                serverManager?.Stop(name);
                var updated = await context.Services
                    .Include(s => s.LocalMachine_Machine)
                    .Include(s => s.RemoteMachine_Machine)
                    .Include(s => s.SpoofMachine_Machine)
                    .FirstAsync(s => s.ServiceId == service.ServiceId);
                serverManager?.AddServer(updated);
                serverManager?.Start(updated.Name);
            }
            AuditLog.Log("Service.Update", name, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }
}
