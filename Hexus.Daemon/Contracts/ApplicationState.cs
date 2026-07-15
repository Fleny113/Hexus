namespace Hexus.Daemon.Contracts;

public enum ApplicationStatus
{
    /// <summary>
    /// The application has a process running
    /// </summary>
    Running,
    /// <summary>
    /// A stop has been requested
    /// </summary>
    Stopping,
    /// <summary>
    /// The application is not running
    /// </summary>
    Stopped,

    /// <summary>
    /// The application is restarting and it is waiting the backoff timer
    /// </summary>
    Restarting,

    /// <summary>
    /// The application has crashed after the restarts
    /// </summary>
    /// <remarks>
    /// This is the only state that is stored to disk
    /// </remarks>
    Crashed,
}
