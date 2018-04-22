using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides handling for a custom command.
    /// </summary>
    internal interface ICommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// the custom command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        Expression ProcessAst(CommandAst commandAst, CompileVisitor visitor);
    }
}
