using API;
using API.Command;
using API.Database;
using API.Plugin;
using API.Server;
using API.ServiceFactory;
using PacketLibrary.Handler;
using PacketLibrary.VSRO188.Gateway.Server;
using SilkroadSecurityAPI.Message;

namespace IPLimit;

public class IPLimit : IPlugin
{
    public void Dispose()
    {
        _serverManager.UnregisterClientHandler<SERVER_GATEWAY_LOGIN_RESPONSE>(ServerType, SERVER_GATEWAY_LOGIN_RESPONSE_HANDLE);
    }

    private IServerManager _serverManager { get; set; }
    private ISharedObjects _sharedObjects { get; set; }
    private int _limit { get; set; }

    public void OnEnable()
    {
        InitSettings();
        _serverManager = ServiceFactory.Load<IServerManager>(typeof(IServerManager));
        _sharedObjects = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
        _limit = int.Parse(DatabaseHelper.GetSettingOrDefault("IPLimit", "4"));
        _serverManager.RegisterModuleHandler<SERVER_GATEWAY_LOGIN_RESPONSE>(ServerType, SERVER_GATEWAY_LOGIN_RESPONSE_HANDLE);
    }

    public void OnServerStart(IAsyncServer server)
    {
    }

    private async Task<Packet> SERVER_GATEWAY_LOGIN_RESPONSE_HANDLE(SERVER_GATEWAY_LOGIN_RESPONSE data,
        ISession session)
    {
        var charsInGameWithSameIp = _sharedObjects.AgentSessions.Count(sess => sess.RemoteEndPoint.Address.Equals(session.RemoteEndPoint.Address));

        if (charsInGameWithSameIp > _limit)
        {
            return data;
        }

        // not implemented
        // data.Result = 0x3;
        // data.BlockType = LoginBlockType.NoAccountInfo;
        data.Status = 100;
        data.ResultType = PacketResultType.Disconnect;
        return data;
    }

    public List<Command> RegisterCommands()
    {
        var result = new List<Command>();
        return result;
    }

    private void InitSettings()
    {
    }

    public string Name => "IPLimit";
    public string Version => "1.0.0";
    public string Author => "b0ykoe";
    public ServerType ServerType => ServerType.GatewayServer;
}