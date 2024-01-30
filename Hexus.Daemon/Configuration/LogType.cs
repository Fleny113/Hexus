using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexus.Daemon.Configuration;

[JsonConverter(typeof(JsonConverter))]
public sealed class LogType
{
    public static readonly LogType System = new("SYSTEM");
    public static readonly LogType StdOut = new("STDOUT");
    public static readonly LogType StdErr = new("STDERR");

    public readonly string Name;

    private LogType(string name)
    {
        Name = name;
    }


    public static bool TryParse(ReadOnlySpan<char> span, [MaybeNullWhen(false)] out LogType result)
    {
        result = span switch
        {
            "SYSTEM" => System,
            "STDOUT" => StdOut,
            "STDERR" => StdErr,
            _ => null,
        };

        return result is not null;
    }

    internal class JsonConverter : JsonConverter<LogType>
    {
        public override LogType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var logTypeString = reader.GetString();

            if (logTypeString is null) return null;

            return TryParse(logTypeString, out var logType)
                ? logType
                : null;
        }

        public override void Write(Utf8JsonWriter writer, LogType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Name);
        }
    }
}
