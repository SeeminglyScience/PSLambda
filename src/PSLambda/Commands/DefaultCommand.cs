using System;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides handling for the "default" custom command.
    /// </summary>
    internal class DefaultCommand : ICommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName { get; } = "default";

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// the "default" command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        public Expression ProcessAst(CommandAst commandAst, CompileVisitor visitor)
        {
            if (commandAst.CommandElements == null || commandAst.CommandElements.Count != 2)
            {
                visitor.TryResolveType(null, out _);
                return Expression.Empty();
            }

            Type resolvedType = null;

            // Is using syntax "default([TypeName])"
            if (commandAst.CommandElements[1] is ParenExpressionAst paren &&
                paren.Pipeline is PipelineAst pipeline &&
                pipeline.PipelineElements.Count == 1 &&
                pipeline.PipelineElements[0] is CommandExpressionAst commandExpression &&
                commandExpression.Expression is TypeExpressionAst &&
                visitor.TryResolveType(commandExpression.Expression, out resolvedType))
            {
                return Expression.Default(resolvedType);
            }

            // Is using syntax "default TypeName"
            if (commandAst.CommandElements[1] is StringConstantExpressionAst stringConstant &&
                LanguagePrimitives.TryConvertTo<Type>(stringConstant.Value, out resolvedType))
            {
                return Expression.Default(resolvedType);
            }

            // Unknown syntax, but this method will produce parse errors for us.
            if (visitor.TryResolveType(commandAst.CommandElements[1], out resolvedType))
            {
                return Expression.Default(resolvedType);
            }

            return Expression.Empty();
        }
    }
}
