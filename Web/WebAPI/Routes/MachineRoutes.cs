using System.Linq;
using System.Threading.Tasks;
using API.Database.Context;
using API.Database.DuckSoup;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using WebApiPlugin.Dto;
using WebApiPlugin;

namespace WebApiPlugin.Routes;

public static class MachineRoutes
{
    public static async Task ListMachines(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            using var context = new DuckSoup();
            var list = await context.Machines
                .Select(m => new MachineDto { MachineId = m.MachineId, Address = m.Address, Notice = m.Notice })
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

    public static async Task AddMachine(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = Newtonsoft.Json.JsonConvert.DeserializeObject<AddMachineRequest>(body);
            if (req == null || string.IsNullOrWhiteSpace(req.Address))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid body or missing address\"}");
                return;
            }
            using var context = new DuckSoup();
            var machine = new Machine { Address = req.Address.Trim(), Notice = req.Notice };
            context.Machines.Add(machine);
            await context.SaveChangesAsync();
            AuditLog.Log("Machine.Add", machine.Address, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 201;
            await ctx.Response.Send(JsonConvert.SerializeObject(new MachineDto { MachineId = machine.MachineId, Address = machine.Address, Notice = machine.Notice }));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task UpdateMachine(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var idStr = ctx.Request.Url.Parameters["id"];
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var id))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing or invalid id parameter\"}");
            return;
        }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateMachineRequest>(body);
            if (req == null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid body\"}");
                return;
            }
            using var context = new DuckSoup();
            var machine = await context.Machines.FindAsync(id);
            if (machine == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Machine not found\"}");
                return;
            }
            if (req.Address != null) machine.Address = req.Address.Trim();
            if (req.Notice != null) machine.Notice = req.Notice;
            await context.SaveChangesAsync();
            AuditLog.Log("Machine.Update", "id=" + id, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(new MachineDto { MachineId = machine.MachineId, Address = machine.Address, Notice = machine.Notice }));
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task DeleteMachine(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin role required\"}"); return; }
        var idStr = ctx.Request.Url.Parameters["id"];
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var id))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing or invalid id parameter\"}");
            return;
        }
        try
        {
            using var context = new DuckSoup();
            var machine = await context.Machines.FindAsync(id);
            if (machine == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Machine not found\"}");
                return;
            }
            var inUse = await context.Services.AnyAsync(s => s.LocalMachine_MachineId == id || s.RemoteMachine_MachineId == id || s.SpoofMachine_MachineId == id);
            if (inUse)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Machine is in use by one or more services\"}");
                return;
            }
            context.Machines.Remove(machine);
            await context.SaveChangesAsync();
            AuditLog.Log("Machine.Delete", "id=" + id, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (System.Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }
}
