namespace Hexus.Daemon.Configuration;

public record ApplicationLog(DateTimeOffset Date, LogType LogType, string Text);
