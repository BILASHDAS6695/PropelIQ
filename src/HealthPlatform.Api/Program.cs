var builder = WebApplication.CreateBuilder(args);

// Layer registrations will be added in Task 005
// builder.Services.AddApplication();
// builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.Run();
