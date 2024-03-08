using API;
using API.Command;
using API.Plugin;
using API.Server;
using API.ServiceFactory;
using Serilog;

namespace ClientlessLogin;

public class ClientlessLogin : IPlugin
{
    public void Dispose()
    {
        _clientManager.Stop();
    }

    private IServerManager _serverManager { get; set; }
    private ClientManager _clientManager { get; set; }

    public void OnEnable()
    {
        InitSettings();
        _serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
        _clientManager = new ClientManager(_serverManager);
    }

    public void OnServerStart(IAsyncServer server)
    {
    }

    public List<Command> RegisterCommands()
    {
        var result = new List<Command>();
        result.Add(new StartCommand(_clientManager));
        result.Add(new StopCommand(_clientManager));
        return result;
    }

    private void InitSettings()
    {
    }

    public string Name => "ClientlessLogin";
    public string Version => "1.0.0";
    public string Author => "b0ykoe";
    public ServerType ServerType => ServerType.None;
}