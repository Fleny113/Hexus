namespace Hexus.Daemon.Contracts.Responses;

public class ProblemDetails
{
    public required string Type { get; set; }
    public required string Title { get; set; }
    public int? Status { get; set; }
    public Dictionary<string, string[]> Errors { get; set; } = [];
}
