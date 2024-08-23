namespace Hexus.Daemon.Contracts;

public record ApplicationLog(DateTimeOffset Date, LogType LogType, string Text)
{
    public bool IsLogDateInRange(DateTimeOffset? before = null, DateTimeOffset? after = null)
    {
        if (before is { } b && Date > b) return false;
        if (after is { } a && Date < a) return false;

        return true;
    }
}
