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

    [Obsolete]
    public static readonly Dictionary<string, string[]> ApplicationAlreadyExists = new()
    {
        {"Name", ["The name is being used by another application."]},
    };

    [Obsolete]
    public static readonly Dictionary<string, string[]> ApplicationRunningWhileEditing = new()
    {
        {"Name", ["The name refers to an application that is running, so it can't be edited."]},
    };
}
