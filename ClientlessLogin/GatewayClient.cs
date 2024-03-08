using System.Net.Sockets;
using API;
using API.Server;
using PacketLibrary.Handler;
using Serilog;
using SilkroadSecurityAPI;
using SilkroadSecurityAPI.Message;
using TcpClient = NetCoreServer.TcpClient;

namespace ClientlessLogin;

public class GatewayClient : TcpClient
{
    private readonly ClientManager _clientManager;
    private ISecurity Security { get; }

    public GatewayClient(IFakeServer fakeServer, ClientManager clientManager) : base(fakeServer.Service.LocalMachine_Machine.Address, fakeServer.Service.BindPort)
    {
        Security = Utility.GetSecurity(SecurityType.VSRO188);
        _clientManager = clientManager;
    }

    protected override void OnConnected()
    {
        Console.WriteLine($"[CC]GatewayClient connected a new session with Id {Id}");
    }

    protected override void OnDisconnected()
    {
        Console.WriteLine($"[CC]GatewayClient disconnected a session with Id {Id}");
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"[CC]GatewayClient caught an error with code {error}");
    }
    
    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            Security.Recv(buffer, (int)offset, (int)size);

            var receivedPackets = Security.TransferIncoming();

            if (receivedPackets == null || receivedPackets.Count == 0) return;

            foreach (var packet in receivedPackets)
            {
                Log.Information("[Server -> [CC]GatewayClient] 0x{0:X}", packet.MsgId);
                switch (packet.MsgId)
                {
                    case 0x6005: // Handshake established
                        var requestServerListPacket = new Packet(0x6101); // shard list request - https://github.com/DummkopfOfHachtenduden/SilkroadDoc/wiki/GATEWAY_SHARD_LIST#request
                        Send(requestServerListPacket);
                        break;
                    case 0xA101: // SERVER_GATEWAY_SHARD_LIST_RESPONSE
                        var loginPacket = new Packet(0x6102); // login - https://github.com/DummkopfOfHachtenduden/SilkroadDoc/wiki/GATEWAY_LOGIN#request
                        loginPacket.TryWrite<byte>(_clientManager.Locale); // locale
                        loginPacket.TryWrite(_clientManager.Username); // username
                        loginPacket.TryWrite(_clientManager.Password); // password
                        loginPacket.TryWrite<ushort>(_clientManager.ShardId); // shard id
                        Send(loginPacket);
                        break;
                    case 0x2322: // SERVER_GATEWAY_LOGIN_IBUV_CHALLENGE
                        var captchaPacket = new Packet(0x6323); // captcha - https://github.com/DummkopfOfHachtenduden/SilkroadDoc/wiki/GATEWAY_LOGIN#ibuv-confirm-request
                        captchaPacket.TryWrite(_clientManager.Captcha);
                        Send(captchaPacket);
                        break;
                    case 0xA102: // SERVER_GATEWAY_LOGIN_RESPONSE 
                        packet.TryRead<byte>(out var result); // result
                        if (result != 0x01)
                        {
                            Log.Information("Something went wrong on login");
                            break;
                        }

                        packet.TryRead<uint>(out var sessionId)
                            .TryRead(out string ip)
                            .TryRead<ushort>(out var port);
                        
                        _clientManager.StartAgent(port, sessionId);

                        break;
                }
            }

            Transfer();
        }
        catch (Exception exception)
        {
            Log.Error("[CC]GatewayClient Recv | {0}", exception.Message);
            Log.Error("[CC]GatewayClient Recv | {0}", exception.StackTrace);
        }
    }

    private void Send(Packet packet, bool transfer = false)
    {
        Security.Send(packet);

        if (transfer) Transfer();
    }

    private void Transfer()
    {
        Security.TransferOutgoing(this);
    }
}