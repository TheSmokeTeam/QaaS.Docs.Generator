using QaaS.Docs.Tools.Infrastructure;

namespace QaaS.Docs.Tools.Commands;

internal interface ICommandHandler
{
    Task<int> ExecuteAsync(DocsToolContext context, CommandArguments arguments);
}
