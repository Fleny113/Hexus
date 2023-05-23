using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public class ListTodosEndpoint : IEndpoint
{
    [HttpMapGet("/todos")]
    public Ok<Todo[]> Handle()
    {
        return TypedResults.Ok(TodoGenerator.SampleTodos);
    }
}