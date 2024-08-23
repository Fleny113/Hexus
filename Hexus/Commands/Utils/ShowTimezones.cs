
using Humanizer.Localisation;
using Humanizer;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Hexus.Commands.Utils;
internal static class ShowTimezones
{
    public static readonly Command Command = new("show-timezones", "Show timezones found in the system. The ID value can be used in the Timezone option for the logs command.");

    static ShowTimezones()
    {
        Command.SetHandler(Handler);
    }

    private static void Handler(InvocationContext context)
    {
        var timezones = TimeZoneInfo.GetSystemTimeZones();
        var localTimezone = TimeZoneInfo.Local;

        PrettyConsole.Out.MarkupLineInterpolated($"""
            Local Timezone information:
             - Display name: "[lightskyblue1]{localTimezone.DisplayName}[/]"
             - ID: "[cyan3]{localTimezone.Id}[/]"
             - Offset: [seagreen3]{localTimezone.BaseUtcOffset.Humanize(precision: 2, minUnit: TimeUnit.Minute)}[/]

             --- All timezones ---

            """);

        foreach (var timezone in timezones)
        {
            PrettyConsole.Out.MarkupLineInterpolated($"""
            Timezone "[lightskyblue1]{timezone.DisplayName}[/]"
             - ID: "[cyan3]{timezone.Id}[/]"
             - Offset: [seagreen3]{timezone.BaseUtcOffset.Humanize(precision: 2)}[/]

            """);
        }
    }
}
