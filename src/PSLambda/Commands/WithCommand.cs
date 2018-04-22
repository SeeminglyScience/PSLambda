using System;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides handling for the "with" custom command.
    /// </summary>
    internal class WithCommand : ICommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName { get; } = "with";

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// the "with" command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        public Expression ProcessAst(CommandAst commandAst, CompileVisitor visitor)
        {
            if (commandAst.CommandElements.Count != 3)
            {
                visitor.ReportParseError(
                    commandAst.Extent,
                    nameof(ErrorStrings.MissingWithElements),
                    ErrorStrings.MissingWithElements);
                return Expression.Empty();
            }

            var bodyAst = commandAst.CommandElements[2] as ScriptBlockExpressionAst;
            if (bodyAst == null)
            {
                visitor.ReportParseError(
                    commandAst.Extent,
                    nameof(ErrorStrings.MissingWithBody),
                    ErrorStrings.MissingWithBody);
                return Expression.Empty();
            }

            var disposeVar = Expression.Variable(typeof(IDisposable));
            return visitor.NewBlock(() =>
                Expression.Block(
                    typeof(void),
                    new[] { disposeVar },
                    Expression.Assign(
                        disposeVar,
                        Expression.Convert(
                            commandAst.CommandElements[1].Compile(visitor),
                            typeof(IDisposable))),
                    Expression.TryFinally(
                        bodyAst.ScriptBlock.EndBlock.Compile(visitor),
                        Expression.Call(disposeVar, ReflectionCache.IDisposable_Dispose))));
        }
    }
}
