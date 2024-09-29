namespace Hexus.Daemon.Contracts;

public record ApplicationLog(DateTimeOffset Date, LogType LogType, string Text);
