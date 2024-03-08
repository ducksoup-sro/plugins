using System.Collections;
using API.Command;

namespace ClientlessLogin;

public class StopCommand : Command
{
    private readonly ClientManager _clientManager;
    
    public StopCommand(ClientManager clientManager) : base("stop", "stop", "Stops the bot", new List<string>())
    {
        _clientManager = clientManager;
    }

    public override void Execute(string[]? args)
    {
        _clientManager.Stop();
    }
}