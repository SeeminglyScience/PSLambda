using System;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides handling for the "generic" custom command.
    /// </summary>
    internal class GenericCommand : ICommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName { get; } = "generic";

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// the "generic" command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        public Expression ProcessAst(CommandAst commandAst, CompileVisitor visitor)
        {
            if (commandAst.CommandElements.Count != 2)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var paren = commandAst.CommandElements[1] as ParenExpressionAst;
            if (paren == null)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var pipeline = paren.Pipeline as PipelineAst;
            if (pipeline == null || pipeline.PipelineElements.Count != 1)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var commandExpression = pipeline.PipelineElements[0] as CommandExpressionAst;
            if (commandExpression == null)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var arrayLiteral = commandExpression.Expression as ArrayLiteralAst;
            if (arrayLiteral.Elements.Count < 2)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var memberExpression = arrayLiteral.Elements[0] as InvokeMemberExpressionAst;
            if (memberExpression == null)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var genericArguments = new Type[arrayLiteral.Elements.Count - 1];
            for (var i = 1; i < arrayLiteral.Elements.Count; i++)
            {
                if (visitor.TryResolveType(arrayLiteral.Elements[i], out Type resolvedType))
                {
                    genericArguments[i - 1] = resolvedType;
                    continue;
                }

                // If a type didn't resolve then a parse error was generated, so exit.
                return Expression.Empty();
            }

            return visitor.CompileInvokeMemberExpression(memberExpression, genericArguments);
        }

        private Expression ReportInvalidSyntax(IScriptExtent extent, CompileVisitor visitor)
        {
            visitor.ReportParseError(
                extent,
                nameof(ErrorStrings.InvalidGenericSyntax),
                ErrorStrings.InvalidGenericSyntax);
            return Expression.Empty();
        }
    }
}
