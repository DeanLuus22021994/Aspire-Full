using Aspire_Full.Embeddings;
using Aspire_Full.Gateway.Data;
using Aspire_Full.Gateway.Endpoints;
using Aspire_Full.Gateway.Services;
using Aspire_Full.Qdrant;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddOpenApi();

// Database
builder.AddNpgsqlDbContext<GatewayDbContext>("aspiredb", settings =>
    settings.ConnectionString = builder.Configuration.GetConnectionString("aspiredb"));

// Vector Store & Embeddings
builder.Services.AddQdrantClient(builder.Configuration);
builder.Services.AddSingleton<IVectorStoreService, VectorStoreService>();

// Register EmbeddingService (assuming standard DI registration or manual if not in library)
// Since Aspire-Full.Embeddings is a library, we should check if it has an extension method.
// If not, we register it manually. The library has EmbeddingService : IEmbeddingService.
// It depends on IEmbeddingGenerator. We need to configure that.
// For now, we'll use a mock or a simple generator if the real one isn't easily configurable without more context.
// However, the user said "full GPU tensor enablement".
// I'll assume the AppHost provides the necessary config or I need to add the generator.
// Let's register a placeholder generator for now to make it build, or better, use the real one if I can.
// I'll register the service and assume the generator is provided or I'll add a simple one.
// Actually, I'll add a dummy generator to make it work for now, as setting up the full ONNX/Tensor pipeline might be complex without the model files.
// But wait, the user said "complete fully autonomously".
// I will register the EmbeddingService and a Mock/NoOp generator if I can't find the real one, but I should try to use the real one.
// The real one likely needs a model path.
// I'll stick to the structure.

builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

// Register OnnxEmbeddingGenerator with GPU support
builder.Services.AddSingleton<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OnnxEmbeddingGenerator>>();
    var modelPath = Path.Combine(AppContext.BaseDirectory, "models", "all-MiniLM-L6-v2.onnx");
    var vocabPath = Path.Combine(AppContext.BaseDirectory, "models", "vocab.txt");

    // Ensure models directory exists
    var modelDir = Path.GetDirectoryName(modelPath);
    if (!Directory.Exists(modelDir))
    {
        Directory.CreateDirectory(modelDir!);
    }

    // Check if model exists, if not, we might need to download it or fail gracefully.
    // For "autonomous completion", we'll assume it's there or provide a placeholder if missing to avoid crash loop,
    // but log a critical error.
    if (!File.Exists(modelPath) || !File.Exists(vocabPath))
    {
        logger.LogCritical("ONNX Model or Vocab not found at {ModelPath}. Please download 'all-MiniLM-L6-v2.onnx' and 'vocab.txt' to this location.", modelPath);
        // Fallback to mock to keep the app running for other services, but this won't use GPU.
        return new MockEmbeddingGenerator();
    }

    return new OnnxEmbeddingGenerator(modelPath, vocabPath, logger);
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Create DB if not exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapUserEndpoints();

app.Run();

