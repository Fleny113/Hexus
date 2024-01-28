namespace Hexus.Daemon.Configuration;

public record ApplicationLog(DateTimeOffset Date, LogType LogType, string Text)
{
    internal const string DateTimeFormat = "MMM dd yyyy HH:mm:ss";
}
