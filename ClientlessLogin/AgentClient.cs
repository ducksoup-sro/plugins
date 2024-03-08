using System.Net.Sockets;
using API;
using API.Server;
using PacketLibrary.Handler;
using PacketLibrary.VSRO188.Agent.Server;
using Serilog;
using SilkroadSecurityAPI;
using SilkroadSecurityAPI.Message;
using TcpClient = NetCoreServer.TcpClient;

namespace ClientlessLogin;

public class AgentClient : TcpClient
{
    private readonly ClientManager _clientManager;
    private ISecurity Security { get; }

    private uint _sessionId;

    public AgentClient(IFakeServer fakeServer, ClientManager clientManager, uint sessionId) : base(
        fakeServer.Service.LocalMachine_Machine.Address, fakeServer.Service.BindPort)
    {
        Security = Utility.GetSecurity(SecurityType.VSRO188);
        _clientManager = clientManager;
        _sessionId = sessionId;
    }

    protected override void OnConnected()
    {
        Console.WriteLine($"[CC]AgentClient connected a new session with Id {Id}");
    }

    protected override void OnDisconnected()
    {
        Console.WriteLine($"[CC]AgentClient] disconnected a session with Id {Id}");
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"[CC]AgentClient] caught an error with code {error}");
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
                Log.Information("[Server -> [CC]AgentClient]] 0x{0:X}", packet.MsgId);
                switch (packet.MsgId)
                {
                    case 0x6005: // SERVER_GLOBAL_NODE_STATUS2 
                        var authPacket = new Packet(0x6103); // CLIENT_AGENT_AUTH_REQUEST 
                        authPacket.TryWrite(_sessionId)
                            .TryWrite(_clientManager.Username)
                            .TryWrite(_clientManager.Password)
                            .TryWrite<byte>(_clientManager.Locale)
                            .TryWrite<uint>(0)
                            .TryWrite<ushort>(0);
                        Send(authPacket);
                        break;
                    case 0xA103: //SERVER_AGENT_AUTH_RESPONSE 
                        packet.TryRead<byte>(out var result);
                        if (result != 0x01)
                            break;
                        var characterJoinPacket = new Packet(0x7007); // CLIENT_AGENT_CHARACTER_SELECTION_ACTION_REQUEST
                        characterJoinPacket.TryWrite<byte>(0x02);
                        Send(characterJoinPacket);
                        break;
                    case 0xB007: //SERVER_AGENT_CHARACTER_SELECTION_ACTION_RESPONSE
                        var charSelectionResponse = packet.CreateCopy<SERVER_CHARACTER_SELECTION_ACTION_RESPONSE>();
                        charSelectionResponse.Read();
                        

                        if (charSelectionResponse.Characters.Count != 0)
                        {
                            var selectCharacter = new Packet(0x7001); //CLIENT_AGENT_CHARACTER_SELECTION_JOIN_REQUEST
                            selectCharacter.TryWrite(charSelectionResponse.Characters.First().Name);
                            Send(selectCharacter);
                        }
                        else
                        {
                            var rnd = new Random();
                            var createCharacterPacket = new Packet(0x7007);
                            createCharacterPacket.TryWrite<byte>(0x1)
                                .TryWrite("admin" + rnd.Next(1, 13))
                                .TryWrite<uint>(1922)
                                .TryWrite<byte>(0)
                                .TryWrite<uint>(3646)
                                .TryWrite<uint>(3647)
                                .TryWrite<uint>(3648)
                                .TryWrite<uint>(3632);
                            Send(createCharacterPacket);
           
                            var characterJoinPacketAfterCreation = new Packet(0x7007); // CLIENT_AGENT_CHARACTER_SELECTION_ACTION_REQUEST
                            characterJoinPacketAfterCreation.TryWrite<byte>(0x02);
                            Send(characterJoinPacketAfterCreation);
                        }

                        break;
                    case 0xB001: // SERVER_AGENT_CHARACTER_SELECTION_JOIN_RESPONSE
                        break;
                    case 0x3013: // chardata
                        Send(new Packet(0x3012)); //AGENT_GAME_READY
                        break;
                    case 0x3026: // SERVER_AGENT_CHAT_UPDATE
                        break;
                }
            }

            Transfer();
        }
        catch (Exception exception)
        {
            Log.Error("[CC]AgentClient] Recv | {0}", exception.Message);
            Log.Error("[CC]AgentClient] Recv | {0}", exception.StackTrace);
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