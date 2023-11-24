using EndpointMapper;
using Hexus.Daemon.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Hexus.Daemon.Endpoints.Applications;

internal class GetLogsEndpoint : IEndpoint
{
    [HttpMap(HttpMapMethod.Get, "/{name}/logs")]
    public static Results<Ok<IEnumerable<string>>, NotFound> Handle(
        [FromRoute] string name,
        [FromQuery] int lines,
        [FromServices] HexusConfiguration configuration
    )
    {
        if (!configuration.Applications.TryGetValue(name, out var application))
            return TypedResults.NotFound();

        return TypedResults.Ok(GetLogs(application, lines));
    }
    
    private static IEnumerable<string> GetLogs(HexusApplication application, int lines)
    {
        lock (application.LogUsageLock)
        {
            using var stream = File.OpenRead($"{EnvironmentHelper.LogsDirectory}/{application.Name}.log");
            var newLineFound = 0;

            if (stream.Length == 0)
                yield break;

            // Go to the end of the file
            stream.Position = stream.Length - 1;

            while (newLineFound <= lines)
            {
                if (stream.ReadByte() != '\n')
                {
                    // We are at the start of the stream, we can not go further behind
                    if (stream.Position <= 2)
                    {
                        stream.Position = 0;
                        break;
                    }

                    // Go back 2 characters, one for the consumed one by ReadByte and one to go actually back and don't re-read the same char each time
                    stream.Seek(-2, SeekOrigin.Current);
                    continue;
                }
                
                newLineFound++;

                // On the last iteration we don't want to move the position
                if (newLineFound <= lines && stream.Position >= 2)
                    stream.Seek(-2, SeekOrigin.Current);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);

            for (var i = 0; i < lines; i++)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                yield return line;
            }
        }
    }
}
