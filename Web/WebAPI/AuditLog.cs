using Microsoft.EntityFrameworkCore;
using WebAPI.Database;
using WebAPI.Database.Entities;

namespace WebAPI;

public static class AuditLog
{
    public static void Log(string action, string? detail, string? username)
    {
        try
        {
            if (!global::Database.DuckContext.ConnectionStrings.ContainsKey(typeof(WebApiContext)))
                return;

            var entry = new AuditEntryEntity
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                Action = action,
                Detail = detail,
                Username = username ?? "anonymous"
            };

            using (var context = new WebApiContext())
            {
                context.AuditEntry.Add(entry);
                context.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[AuditLog] Failed to write audit entry");
        }

        Serilog.Log.ForContext("Source", "Interface").Information("[WebAPI] {Action} | {Detail} | by {User}", action, detail ?? "", username ?? "?");
    }

    public static async Task<List<AuditEntryDto>> GetRecentAsync(int? limit = 100, string? actionPrefix = null)
    {
        if (!global::Database.DuckContext.ConnectionStrings.ContainsKey(typeof(WebApiContext)))
            return new List<AuditEntryDto>();

        await using var context = new WebApiContext();
        var query = context.AuditEntry.OrderByDescending(e => e.Id).AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionPrefix))
        {
            var prefix = actionPrefix.Trim();
            query = query.Where(e => e.Action != null && e.Action.StartsWith(prefix));
        }

        var take = Math.Min(limit ?? 100, 500);
        var list = await query.Take(take).ToListAsync();

        return list
            .Select(e => new AuditEntryDto { Timestamp = e.Timestamp, Action = e.Action, Detail = e.Detail, Username = e.Username })
            .ToList();
    }

    public class AuditEntryDto
    {
        public string Timestamp { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Detail { get; set; }
        public string Username { get; set; } = "";
    }
}
