var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "Aspire-Full Web Frontend");
app.Run();
