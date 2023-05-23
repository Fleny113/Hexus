using EndpointMapper;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hexus.Daemon.Endpoints;

public class GetTodoEndpoint : IEndpoint
{
    [HttpMapGet("/todo/{id:int}")]
    public Results<Ok<Todo>, NotFound> Handle(int id)
    {
        return TodoGenerator.SampleTodos.FirstOrDefault(a => a.Id == id) is not { } todo
            ? TypedResults.NotFound()
            : TypedResults.Ok(todo);
    }
}