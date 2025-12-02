// Global usings for AI/Aspire-Full.Embeddings
// Re-exports types from Infra/Aspire-Full.Connectors for backward compatibility

// Embedding types from Infra/Connectors
global using Aspire_Full.Connectors.Embeddings;
global using Aspire_Full.Connectors.DependencyInjection;

// Re-export commonly used types for legacy code
global using IEmbeddingService = Aspire_Full.Connectors.Embeddings.IEmbeddingService;
global using EmbeddingService = Aspire_Full.Connectors.Embeddings.EmbeddingService;
global using OnnxEmbeddingGenerator = Aspire_Full.Connectors.Embeddings.OnnxEmbeddingGenerator;
global using MockEmbeddingGenerator = Aspire_Full.Connectors.Embeddings.MockEmbeddingGenerator;
