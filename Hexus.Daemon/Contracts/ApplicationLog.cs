using System.Text;

namespace Hexus.Daemon.Contracts;

public record ApplicationLog(DateTimeOffset Date, LogType LogType, string Text)
{
    public const string ApplicationStartedLog = "-- Application started --";
    public static readonly CompositeFormat ApplicationStoppedLog = CompositeFormat.Parse("-- Application stopped [Exit code: {0}] --");
}
