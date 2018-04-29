using System.Globalization;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides a base for commands that follow the format of
    /// `commandName ($objectTarget) { body }`
    /// </summary>
    internal abstract class ObjectAndBodyCommandHandler : ICommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public abstract string CommandName { get; }

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// a custom command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        public Expression ProcessAst(CommandAst commandAst, CompileVisitor visitor)
        {
            if (commandAst.CommandElements.Count != 3)
            {
                visitor.Errors.ReportParseError(
                    commandAst.Extent,
                    nameof(ErrorStrings.MissingKeywordElements),
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ErrorStrings.MissingKeywordElements,
                        CommandName));
                return Expression.Empty();
            }

            var bodyAst = commandAst.CommandElements[2] as ScriptBlockExpressionAst;
            if (bodyAst == null)
            {
                visitor.Errors.ReportParseError(
                    commandAst.Extent,
                    nameof(ErrorStrings.MissingKeywordBody),
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ErrorStrings.MissingKeywordBody,
                        CommandName));
                return Expression.Empty();
            }

            return ProcessObjectAndBody(
                commandAst,
                commandAst.CommandElements[1],
                bodyAst,
                visitor);
        }

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// a custom command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="targetAst">The AST containing the target of the keyword.</param>
        /// <param name="bodyAst">The AST containing the body of the keyword.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        protected abstract Expression ProcessObjectAndBody(
            CommandAst commandAst,
            CommandElementAst targetAst,
            ScriptBlockExpressionAst bodyAst,
            CompileVisitor visitor);
    }
}
