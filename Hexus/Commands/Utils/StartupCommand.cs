using Hexus.Daemon.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.Diagnostics;
using System.Security.Principal;

namespace Hexus.Commands.Utils;

internal static class StartupCommand
{
    private static readonly Option<bool> UseSystemdSystem = new("--system")
    {
        Description = "Only for Linux Systemd. Generate a System Unit instead of a User Unit",
    };

    public static readonly Command Command = new("startup", "Setup the hexus daemon to run on startup")
    {
        UseSystemdSystem,
    };

    private const string PowershellVariableColor = "#16c60c";
    private const string PowershellCmdletColor = "#f9f1a5";
    private const string PowershellOperatorColor = "#767676";
    private const string PowershellStringColor = "#3a96dd";
    private const string PowershellCommentColor = "#13a10e";

    private const string UnitSectionColor = "#afd75f";
    private const string UnitKeyColor = "#d85252";
    private const string UnitValueColor = "#d7d787";

    static StartupCommand()
    {
        Command.SetAction(Handler);
    }

    private static int Handler(ParseResult parseResult)
    {
        var systemdSystem = parseResult.GetValue(UseSystemdSystem);
        var cliExecutable = Process.GetCurrentProcess().MainModule?.FileName;
        var username = Environment.UserName;

        if (cliExecutable is null)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] in getting the file location for hexus");
            return 1;
        }

        var daemonExe = $"{Path.GetDirectoryName(cliExecutable)}{Path.DirectorySeparatorChar}hexusd{Path.GetExtension(cliExecutable)}";

        if (!Path.Exists(daemonExe))
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] in getting the file location for hexusd");
            return 1;
        }

        var startRule = new Rule()
            .Centered()
            .RuleStyle(Color.Gold1);

        var endRule = new Rule()
            .RuleStyle(Color.Gold1);

        if (OperatingSystem.IsWindows())
        {
            var windowsUser = WindowsIdentity.GetCurrent().Name;
            var description = $"Hexus process manager daemon for user {username}";
            var powershellCommand = $"""
            {Comment("# Use the following powershell script in a elevated powershell prompt to create the scheduled task for run hexus:")}

            {Variable("$action")} {Operator("=")} {Cmdlet("New-ScheduledTaskAction")} {Operator("-Execute")} {String(daemonExe)} {Operator("-WorkingDirectory")} {String(EnvironmentHelper.Home)}
            {Variable("$trigger")} {Operator("=")} {Cmdlet("New-ScheduledTaskTrigger")} {Operator("-AtLogon")}
            {Variable("$principal")} {Operator("=")} {Cmdlet("New-ScheduledTaskPrincipal")} {Operator("-UserId")} {String(windowsUser)} {Operator("-LogonType")} S4U {Operator("-RunLevel")} Limited
            {Variable("$settings")} {Operator("=")} {Cmdlet("New-ScheduledTaskSettingsSet")} {Operator("-Compatibility")} Win8 {Operator("-MultipleInstances")} IgnoreNew {Operator("-Hidden")}
            {Variable("$task")} {Operator("=")} {Cmdlet("New-ScheduledTask")} {Operator("-Action")} {Variable("$action")} {Operator("-Principal")} {Variable("$principal")} {Operator("-Trigger")} {Variable("$trigger")} {Operator("-Settings")} {Variable("$settings")} {Operator("-Description")} {String(description)}
            {Cmdlet("Register-ScheduledTask")} {Operator("-TaskName")} {String($"hexus-{username.ToLower()}")} {Operator("-TaskPath")} {String("\\")} {Operator("-InputObject")} {Variable("$task")}
            """;

            startRule.RuleTitle("[white]Startup powershell script[/]");

            if (Console.IsOutputRedirected)
            {
                PrettyConsole.OutLimitlessWidth.MarkupLine(powershellCommand);
                return 0;
            }

            PrettyConsole.Out.Write(startRule);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.OutLimitlessWidth.MarkupLine(powershellCommand);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.Out.Write(endRule);

            return 0;

            string Variable(string variable) => Console.IsOutputRedirected ? variable.EscapeMarkup() : $"[{PowershellVariableColor}]{variable.EscapeMarkup()}[/]";
            string Cmdlet(string cmdlet) => Console.IsOutputRedirected ? cmdlet.EscapeMarkup() : $"[{PowershellCmdletColor}]{cmdlet.EscapeMarkup()}[/]";
            string Operator(string @operator) => Console.IsOutputRedirected ? @operator.EscapeMarkup() : $"[{PowershellOperatorColor}]{@operator.EscapeMarkup()}[/]";
            string String(string @string) => Console.IsOutputRedirected ? $"\"{@string.EscapeMarkup()}\"" : $"[{PowershellStringColor}]\"{@string.EscapeMarkup()}\"[/]";
            string Comment(string comment) => Console.IsOutputRedirected ? comment.EscapeMarkup() : $"[{PowershellCommentColor}]{comment.EscapeMarkup()}[/]";
        }

        if (OperatingSystem.IsLinux())
        {
            // The User field on the Serive section needs to be conditional based on if we are generating a System Unit or User unit
            // Setting the User field when using a User unit will make the unit error when starting
            var unitFile = $"""
            [[{Section("Unit")}]]
            {Key("Description")}={Value(systemdSystem ? $"Hexus process manager for user {username}" : "Hexus process manager")}
            {Key("After")}={Value("network.target")}

            [[{Section("Service")}]]
            {Key("Type")}={Value("notify")}{(systemdSystem ? $"\n{Key("User")}={Value($"{username}")}" : "")}
            {Key("TasksMax")}={Value("infinity")}
            {Key("Restart")}={Value("on-failure")}
            {Key("ExecStart")}={Value(daemonExe)}
            {Key("TimeoutStopSec")}={Value("1min")}

            [[{Section("Install")}]]
            {Key("WantedBy")}={Value(systemdSystem ? "multi-user.target" : "default.target")}
            """;

            startRule.RuleTitle($"[white]Startup {(systemdSystem ? "system" : "user")} systemd service[/]");

            if (Console.IsOutputRedirected)
            {
                PrettyConsole.OutLimitlessWidth.MarkupLine(unitFile);
                return 0;
            }

            PrettyConsole.Out.Write(startRule);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.OutLimitlessWidth.MarkupLine(unitFile);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.Out.Write(endRule);

            if (!systemdSystem)
            {
                PrettyConsole.Out.MarkupLine("""
                [yellow1]Warning[/]: The generated unit file above is intended to be used as a User Unit, using it as a System unit will result in Hexus running as root and/or erroring.
                To manage user units, please use the '--user' flag in systemctl commands without sudo (the '--user' flag refers to the executing user).
                
                To run Hexus at boot without requiring an active, please enable lingering for your user (via 'loginctl enable-linger') and enable the unit.

                If you prefer to use a system unit pass the '--system' option to this command
                """);
            }
            else
            {
                PrettyConsole.Out.MarkupLine("""
                 [yellow1]Warning[/]: The generated unit file above is intended to be used as a System Unit, using it as a User unit will result in an error

                 You may have issues connecting to the daemon if you don't specify manually the unix socket location in the config file.
                 This is due to the XDG_RUNTIME_DIR envrionment variable not being available in a System Unit.

                 If you prefer to use a user unit do not pass the '--system' option to this command.
                 """);
            }

            return 0;

            string Section(string section) => Console.IsOutputRedirected ? section.EscapeMarkup() : $"[{UnitSectionColor}]{section.EscapeMarkup()}[/]";
            string Key(string key) => Console.IsOutputRedirected ? key.EscapeMarkup() : $"[{UnitKeyColor}]{key.EscapeMarkup()}[/]";
            string Value(string value) => Console.IsOutputRedirected ? value.EscapeMarkup() : $"[{UnitValueColor}]{value.EscapeMarkup()}[/]";
        }

        throw new NotSupportedException("The generation of the startup code is available only for Linux (systemd) and Windows");
    }
}
