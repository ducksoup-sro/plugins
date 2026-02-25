namespace WebAPI.Database.Entities;

public class AuditEntryEntity
{
    public int Id { get; set; }
    public string Timestamp { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public string Username { get; set; } = "";
}
