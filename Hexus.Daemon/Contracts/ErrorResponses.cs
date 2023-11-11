namespace Hexus.Daemon.Contracts;

public static class ErrorResponses
{
    public static readonly ErrorResponse ApplicationIsNotRunningMessage = new(Error: "The application is not running.");
    public static readonly ErrorResponse ApplicationIsRunningMessage = new(Error: "The application is already running.");
    public static readonly ErrorResponse ApplicationWithTheSameNameAlreadyExiting = new(Error: "There is already an application with the same name.");
    public static readonly ErrorResponse CantEditRunningApplication = new(Error: "To edit an application, the application must not be running.");

}
