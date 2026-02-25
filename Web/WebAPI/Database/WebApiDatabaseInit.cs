using API.ServiceFactory;
using API.Settings;
using API.Webserver;
using Database;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace WebAPI.Database;

public static class WebApiDatabaseInit
{
    public static async Task InitDatabaseAsync(IWebserverManager webserverManager)
    {
        try
        {
            var settings = ServiceFactory.Load<ISettingsManager>(typeof(ISettingsManager)).Settings;
            var connStr = $"data source={settings.Address},{settings.Port};initial catalog={settings.ProxyDb};persist security info=True;User Id={settings.Username};Password={settings.Password};MultipleActiveResultSets=True;App=DuckSoupEntityFramework;Encrypt=False;";

            DuckContext.ConnectionStrings[typeof(WebApiContext)] = connStr;

            await using (var context = new WebApiContext())
            {
                await context.Database.MigrateAsync();
            }

            await LoadCorsFromCoreDbAsync(webserverManager);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WebApi] Database init / migration failed");
            throw;
        }
    }

    /// <summary>Load CORS origins from core database (API.Database) and register with webserver.</summary>
    public static async Task LoadCorsFromCoreDbAsync(IWebserverManager webserverManager)
    {
        if (!DuckContext.ConnectionStrings.ContainsKey(typeof(API.Database.Context.DuckSoup)))
            return;

        await using var context = new API.Database.Context.DuckSoup();
        var origins = await context.CorsOrigins.Select(o => o.Origin).ToListAsync();
        foreach (var origin in origins)
            webserverManager.AddAllowedOrigin(origin);
    }
}
