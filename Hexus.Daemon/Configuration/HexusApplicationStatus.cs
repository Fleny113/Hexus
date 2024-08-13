namespace Hexus.Daemon.Configuration;

public enum HexusApplicationStatus
{
    Crashed = -2,
    Exited = -1,
    Running = 0,

    Restarting = 1,
    Stopping = 2,
}
