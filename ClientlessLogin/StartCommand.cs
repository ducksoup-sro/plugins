using System.Collections;
using API.Command;

namespace ClientlessLogin;

public class StartCommand : Command
{
    private readonly ClientManager _clientManager;
    
    public StartCommand(ClientManager clientManager) : base("start", "start", "Starts the bot", new List<string>())
    {
        _clientManager = clientManager;
    }

    public override void Execute(string[]? args)
    {
        _clientManager.StartGateway();
    }
}