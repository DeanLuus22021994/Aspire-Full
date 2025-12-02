using ArcFaceSandbox.UsersKernel.Infrastructure.Entities;
using ArcFaceSandbox.UsersKernel.Infrastructure.Models;

namespace ArcFaceSandbox.UsersKernel.Infrastructure.Services;

public interface ISandboxUserService
{
    Task<SandboxUser> UpsertAsync(UserUpsertCommand command, CancellationToken cancellationToken = default);

    Task<SandboxUser?> UpdateAsync(Guid id, UserUpdateCommand command, CancellationToken cancellationToken = default);

    Task<bool> DownsertAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SandboxUser?> RecordLoginAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SandboxUser?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);
}
