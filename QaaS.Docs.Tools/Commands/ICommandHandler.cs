using QaaS.Docs.Tools.Infrastructure;

namespace QaaS.Docs.Tools.Commands;

/// <summary>
/// Represents a single docs-maintenance command exposed by <c>QaaS.Docs.Tools</c>.
/// </summary>
internal interface ICommandHandler
{
    /// <summary>
    /// Executes the command using the resolved workspace context and parsed command-line arguments.
    /// </summary>
    Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments);
}
