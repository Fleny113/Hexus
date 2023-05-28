namespace Hexus.Daemon;

public sealed record HexusApplication
{
    public required string Name { get; set; }
    public required string Executable { get; set; }
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
}
