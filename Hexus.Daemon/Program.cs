using EndpointMapper;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddEndpointMapper<Program>();

var app = builder.Build();

app.UseEndpointMapper();

app.Run();

