// Global usings for AI/Aspire-Full.Tensor
// Re-exports types from Core/Shared and Infra/Tensor.Core for backward compatibility

// Tensor DTOs from Shared
global using TensorInferenceChunkDto = Aspire_Full.Shared.Models.TensorInferenceChunk;
global using TensorJobStatusDto = Aspire_Full.Shared.Models.TensorJobStatus;
global using TensorJobSubmissionDto = Aspire_Full.Shared.Models.TensorJobSubmission;
global using TensorJobSummaryDto = Aspire_Full.Shared.Models.TensorJobSummary;
global using TensorModelSummaryDto = Aspire_Full.Shared.Models.TensorModelSummary;
global using TensorModelDescriptor = Aspire_Full.Shared.Models.TensorModelDescriptor;
global using TensorModelCatalogOptions = Aspire_Full.Shared.Models.TensorModelCatalogOptions;
global using TensorCapabilityResponse = Aspire_Full.Shared.Models.TensorCapabilityResponse;

// Diagnostics from Infra/Tensor.Core
global using TensorDiagnostics = Aspire_Full.Tensor.Core.TensorDiagnostics;
