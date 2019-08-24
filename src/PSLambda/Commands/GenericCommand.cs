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

            if (!(commandAst.CommandElements[1] is ParenExpressionAst paren))
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            if (!(paren.Pipeline is PipelineAst pipeline) || pipeline.PipelineElements.Count != 1)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            if (!(pipeline.PipelineElements[0] is CommandExpressionAst commandExpression))
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            var arrayLiteral = commandExpression.Expression as ArrayLiteralAst;
            if (arrayLiteral.Elements.Count < 2)
            {
                return ReportInvalidSyntax(commandAst.Extent, visitor);
            }

            if (!(arrayLiteral.Elements[0] is InvokeMemberExpressionAst memberExpression))
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
            visitor.Errors.ReportParseError(
                extent,
                nameof(ErrorStrings.InvalidGenericSyntax),
                ErrorStrings.InvalidGenericSyntax);
            return Expression.Empty();
        }
    }
}
