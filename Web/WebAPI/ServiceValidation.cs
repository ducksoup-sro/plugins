using API.Database.Context;
using Microsoft.EntityFrameworkCore;
using SilkroadSecurityAPI;

namespace WebAPI;

public static class ServiceValidation
{
    public static bool IsValidServerType(int value)
    {
        return value >= (int)API.ServerType.DownloadServer && value <= (int)API.ServerType.AgentServer;
    }

    public static bool IsValidSecurityType(int value)
    {
        return value == (int)SecurityType.VSRO188 || value == (int)SecurityType.ISRO_R;
    }

    public static async Task<string?> ValidatePortsAsync(int localMachineId, int bindPort, int remotePort, int? excludeServiceId = null)
    {
        await using var context = new DuckSoup();
        var query = context.Services.Where(s =>
            s.LocalMachine_MachineId == localMachineId &&
            (s.BindPort == bindPort || s.RemotePort == remotePort));
        if (excludeServiceId.HasValue)
            query = query.Where(s => s.ServiceId != excludeServiceId.Value);
        var existing = await query.FirstOrDefaultAsync();
        if (existing != null)
            return $"Port already in use by service '{existing.Name}' (BindPort={existing.BindPort}, RemotePort={existing.RemotePort}).";
        return null;
    }
}
