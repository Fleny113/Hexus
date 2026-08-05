namespace Hexus.Daemon.Contracts;

public static class ErrorResponses
{
    public static readonly Dictionary<string, string[]> ApplicationNotRunning = new()
    {
        {"Name", ["The name refers to an application that is not running."]},
    };

    public static readonly Dictionary<string, string[]> ApplicationAlreadyRunning = new()
    {
        {"Name", ["The name refers to an application that is already running."]},
    };
}
