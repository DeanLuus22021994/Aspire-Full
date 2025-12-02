using Aspire_Full.Shared.Models;

namespace Aspire_Full.Agents.Core.Catalog;

/// <summary>
/// Provides access to subagent definitions by role.
/// </summary>
public interface ISubagentCatalog
{
    /// <summary>
    /// Gets all registered subagent definitions.
    /// </summary>
    IEnumerable<SubagentDefinition> All { get; }

    /// <summary>
    /// Gets the definition for a specific subagent role.
    /// </summary>
    /// <param name="role">The role to retrieve.</param>
    /// <returns>The subagent definition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the role is not found.</exception>
    SubagentDefinition Get(SubagentRole role);

    /// <summary>
    /// Attempts to get the definition for a specific subagent role.
    /// </summary>
    /// <param name="role">The role to retrieve.</param>
    /// <param name="definition">The definition if found.</param>
    /// <returns>True if the definition was found; otherwise, false.</returns>
    bool TryGet(SubagentRole role, out SubagentDefinition? definition);
}
