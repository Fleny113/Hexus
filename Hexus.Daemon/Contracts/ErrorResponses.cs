namespace Hexus.Daemon.Contracts;

public static class ErrorResponses
{
    public static readonly ErrorResponse ApplicationIsNotRunningMessage = new("The application is not running.");
    public static readonly ErrorResponse ApplicationIsRunningMessage = new("The application is already running.");
    public static readonly ErrorResponse ApplicationWithTheSameNameAlreadyExiting = new("There is already an application with the same name.");
    public static readonly ErrorResponse CantEditRunningApplication = new("To edit an application, the application must not be running.");
}
