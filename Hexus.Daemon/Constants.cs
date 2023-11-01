namespace Hexus.Daemon;

public static class Constants
{
    public static readonly object ApplicationWithTheSameNameAlreadyExiting = new { Error = "There is already an application with the same name." };
    public static readonly object ApplicationIsNotRunningMessage = new { Error = "The application is not running." };
    public static readonly object ApplicationIsRunningMessage = new { Error = "The application is already running." };
}
