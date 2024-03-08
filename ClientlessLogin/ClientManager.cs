using API;
using API.Server;

namespace ClientlessLogin;

public class ClientManager
{
    private AgentClient _agentClient;
    private GatewayClient _gatewayClient;
    private readonly IServerManager _serverManager;
    
    // TODO :: hardcoded, needs a database or sum stuff
    public string Username { get; } = "";
    public string Password { get; } = "";
    public byte Locale { get; } = 22;
    public ushort ShardId { get; } = 3;
    public string Captcha { get; } = "0";
    
    public ClientManager(IServerManager serverManager)
    {
        _serverManager = serverManager;
    }

    private IFakeServer GetGateway()
    {
        return _serverManager.Servers.FirstOrDefault(server => server.Service.ServerType == ServerType.GatewayServer)  ?? throw new Exception("no gateway was found.");
    }

    private IFakeServer GetAgent(ushort port)
    {
        return _serverManager.Servers.FirstOrDefault(server => server.Service.ServerType == ServerType.AgentServer && server.Service.BindPort == port) 
                      ?? throw new Exception("no agent was found.");
    }

    public void StartGateway()
    {
        _gatewayClient = new GatewayClient(GetGateway(), this);
        _gatewayClient.ConnectAsync();
    }
    
    public void StartAgent(ushort port, uint sessionId)
    {
        _agentClient = new AgentClient(GetAgent(port), this, sessionId);
        _agentClient.ConnectAsync();
    }

    public void Stop()
    {
        _gatewayClient.DisconnectAsync();
        while (_gatewayClient.IsConnected)
            Thread.Yield();

        _agentClient.DisconnectAsync();
        while (_agentClient.IsConnected)
            Thread.Yield();
    }
    
}