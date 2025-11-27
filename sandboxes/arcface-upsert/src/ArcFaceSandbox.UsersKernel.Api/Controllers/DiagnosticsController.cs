using System.Globalization;
using ArcFaceSandbox.EmbeddingService;
using ArcFaceSandbox.UsersKernel.Infrastructure.Data;
using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;
using ArcFaceSandbox.VectorStore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArcFaceSandbox.UsersKernel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DiagnosticsController : ControllerBase
{
    private readonly IArcFaceEmbeddingService _embeddingService;
    private readonly SandboxUsersDbContext _dbContext;
    private readonly ISandboxVectorStore _vectorStore;
    private readonly ArcFaceEmbeddingOptions _embeddingOptions;
    private readonly SandboxVectorStoreOptions _vectorOptions;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        IArcFaceEmbeddingService embeddingService,
        SandboxUsersDbContext dbContext,
        ISandboxVectorStore vectorStore,
        IOptions<ArcFaceEmbeddingOptions> embeddingOptions,
        IOptions<SandboxVectorStoreOptions> vectorOptions,
        ILogger<DiagnosticsController> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _embeddingOptions = embeddingOptions?.Value ?? throw new ArgumentNullException(nameof(embeddingOptions));
        _vectorOptions = vectorOptions?.Value ?? throw new ArgumentNullException(nameof(vectorOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("embedding")]
    [ProducesResponseType(typeof(EmbeddingDiagnosticsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmbeddingDiagnosticsResponse>> GetEmbeddingStatus(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .IgnoreQueryFilters()
            .OrderByDescending(u => u.UpdatedAt)
            .Select(u => new { u.IsActive, u.UpdatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var response = new EmbeddingDiagnosticsResponse(
            _embeddingService.ModelInfo,
            _embeddingOptions.ModelPath,
            System.IO.File.Exists(_embeddingOptions.ModelPath),
            users.Count(u => u.IsActive),
            users.Count,
            users.FirstOrDefault()?.UpdatedAt);

        return Ok(response);
    }

    [HttpGet("vector-store")]
    [ProducesResponseType(typeof(VectorStoreStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VectorStoreStatusResponse>> GetVectorStoreStatus(CancellationToken cancellationToken)
    {
        var issues = new List<VectorStoreIssue>();
        var documents = new List<VectorDocumentStatus>();
        var isReachable = true;

        try
        {
            await _vectorStore.EnsureCollectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            isReachable = false;
            issues.Add(new VectorStoreIssue("collection_init_failed", ex.Message));
            _logger.LogWarning(ex, "Unable to reach sandbox vector store");
        }

        if (isReachable)
        {
            var trackedUsers = await _dbContext.Users
                .IgnoreQueryFilters()
                .OrderByDescending(u => u.UpdatedAt)
                .Take(25)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var user in trackedUsers)
            {
                var documentId = GetDocumentId(user.Id);
                try
                {
                    var document = await _vectorStore.GetAsync(documentId, cancellationToken).ConfigureAwait(false);
                    documents.Add(VectorDocumentStatus.FromUser(user, documentId, document));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read vector document {DocumentId}", documentId);
                    issues.Add(new VectorStoreIssue("document_probe_failed", $"Failed to read document for user {user.Email}: {ex.Message}"));
                }
            }
        }

        var status = new VectorStoreStatusResponse(
            _vectorOptions.Endpoint,
            _vectorOptions.CollectionName,
            _vectorOptions.VectorSize,
            _vectorOptions.AutoCreateCollection,
            isReachable,
            documents,
            issues);

        return Ok(status);
    }

    private static string GetDocumentId(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);
}

public sealed record EmbeddingDiagnosticsResponse(
    ArcFaceModelInfo ModelInfo,
    string ModelPath,
    bool ModelFileExists,
    int ActiveUsers,
    int TotalUsers,
    DateTime? LastUserChangeUtc);

public sealed record VectorStoreStatusResponse(
    string Endpoint,
    string CollectionName,
    int VectorSize,
    bool AutoCreateCollection,
    bool IsReachable,
    IReadOnlyList<VectorDocumentStatus> Documents,
    IReadOnlyList<VectorStoreIssue> Issues);

public sealed record VectorDocumentStatus(
    Guid UserId,
    string UserEmail,
    string DisplayName,
    string VectorDocumentId,
    bool VectorExists,
    bool IsDeleted,
    DateTime? VectorUpdatedAt,
    DateTime? VectorDeletedAt)
{
    public static VectorDocumentStatus FromUser(SandboxUser user, string documentId, SandboxVectorDocument? document)
    {
        var vectorExists = document is not null;
        var isDeleted = document?.IsDeleted ?? false;
        return new VectorDocumentStatus(
            user.Id,
            user.Email,
            user.DisplayName,
            documentId,
            vectorExists,
            isDeleted,
            document?.UpdatedAt,
            document?.DeletedAt);
    }
}

public sealed record VectorStoreIssue(string Code, string Message);
