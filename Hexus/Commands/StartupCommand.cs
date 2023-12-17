using Hexus.Daemon.Configuration;
using Spectre.Console;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Security.Principal;

namespace Hexus.Commands;

internal static class StartupCommand
{
    public static readonly Command Command = new("startup", "Setup the hexus daemon to run on startup");

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
        Command.SetHandler(Handler);
    }

    private static void Handler(InvocationContext context)
    {
        var executable = Process.GetCurrentProcess().MainModule?.FileName;
        var username = Environment.UserName;
        
        if (executable is null)
        {
            PrettyConsole.Error.MarkupLine("There [indianred1]was an error[/] in getting the filename for Hexus");
            context.ExitCode = 1;
            return;
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

            {Variable("$action")} {Operator("=")} {Cmdlet("New-ScheduledTaskAction")} {Operator("-Execute")} {String(executable)} {Operator("-Argument")} {String("daemon start")} {Operator("-WorkingDirectory")} {String(EnvironmentHelper.Home)}
            {Variable("$trigger")} {Operator("=")} {Cmdlet("New-ScheduledTaskTrigger")} {Operator("-AtLogon")}
            {Variable("$principal")} {Operator("=")} {Cmdlet("New-ScheduledTaskPrincipal")} {Operator("-UserId")} {String(windowsUser)} {Operator("-LogonType")} S4U {Operator("-RunLevel")} Limited
            {Variable("$settings")} {Operator("=")} {Cmdlet("New-ScheduledTaskSettingsSet")} {Operator("-Compatibility")} Win8 {Operator("-MultipleInstances")} IgnoreNew  {Operator("-Hidden")}
            {Variable("$task")} {Operator("=")} {Cmdlet("New-ScheduledTask")} {Operator("-Action")} {Variable("$action")} {Operator("-Principal")} {Variable("$principal")} {Operator("-Trigger")} {Variable("$trigger")} {Operator("-Settings")} {Variable("$settings")} {Operator("-Description")} {String(description)}
            {Cmdlet("Register-ScheduledTask")} {Operator("-TaskName")} {String($"hexus-{username.ToLower()}")} {Operator("-TaskPath")} {String("\\")} {Operator("-InputObject")} {Variable("$task")}
            """;

            startRule.RuleTitle("[white]Startup powershell script[/]");

            if (Console.IsOutputRedirected)
            {
                PrettyConsole.Out.MarkupLine(powershellCommand);
                return;
            }
            
            PrettyConsole.Out.Write(startRule);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.Out.MarkupLine(powershellCommand);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.Out.Write(endRule);
            
            return;
            
            string Variable(string variable) => Console.IsOutputRedirected ? variable.EscapeMarkup() : $"[{PowershellVariableColor}]{variable.EscapeMarkup()}[/]"; 
            string Cmdlet(string cmdlet) => Console.IsOutputRedirected ? cmdlet.EscapeMarkup() : $"[{PowershellCmdletColor}]{cmdlet.EscapeMarkup()}[/]"; 
            string Operator(string @operator) => Console.IsOutputRedirected ? @operator.EscapeMarkup() : $"[{PowershellOperatorColor}]{@operator.EscapeMarkup()}[/]"; 
            string String(string @string) => Console.IsOutputRedirected ? $"\"{@string.EscapeMarkup()}\"" : $"[{PowershellStringColor}]\"{@string.EscapeMarkup()}\"[/]"; 
            string Comment(string comment) => Console.IsOutputRedirected ? comment.EscapeMarkup() :  $"[{PowershellCommentColor}]{comment.EscapeMarkup()}[/]";
        }

        if (OperatingSystem.IsLinux())
        {
            var unitFile = $"""
            [[{Section("Unit")}]]
            {Key("Description")}={Value($"Hexus process manager for user {username}")}
            {Key("After")}={Value("network.target")}
            
            [[{Section("Service")}]]
            {Key("Type")}={Value("exec")}
            {Key("User")}={Value($"{username}")}
            {Key("TasksMax")}={Value("infinity")}
            {Key("Restart")}={Value("on-failure")}
            {Key("ExecStart")}={Value($"{executable} daemon start")}
            {Key("ExecStop")}={Value($"{executable} daemon stop")}
            
            [[{Section("Install")}]]
            {Key("WantedBy")}={Value("multi-user.target")}
            """;
            
            startRule.RuleTitle("[white]Startup systemd service[/]");
            
            if (Console.IsOutputRedirected)
            {
                PrettyConsole.Out.MarkupLine(unitFile);
                return;
            }
            
            PrettyConsole.Out.Write(startRule);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.Out.MarkupLine(unitFile);
            PrettyConsole.Out.WriteLine();
            PrettyConsole.Out.Write(endRule);
            
            return;
            
            string Section(string section) => Console.IsOutputRedirected ? section.EscapeMarkup() : $"[{UnitSectionColor}]{section.EscapeMarkup()}[/]";
            string Key(string key) => Console.IsOutputRedirected ? key.EscapeMarkup() : $"[{UnitKeyColor}]{key.EscapeMarkup()}[/]"; 
            string Value(string value) => Console.IsOutputRedirected ? value.EscapeMarkup() : $"[{UnitValueColor}]{value.EscapeMarkup()}[/]"; 
        }

        throw new NotSupportedException("The generation of the startup code is available only for Linux (systemd) and Windows");
    }
}
