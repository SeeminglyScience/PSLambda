using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides utility methods for creating <see cref="Expression" /> objects.
    /// </summary>
    internal static class ExpressionExtensions
    {
        private static readonly Expression[] s_emptyExpressions = new Expression[0];

        /// <summary>
        /// Compile all asts in a given <see cref="IList{TAst}" />.
        /// </summary>
        /// <param name="asts">The <see cref="Ast" /> objects to compile.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the compile.</param>
        /// <typeparam name="TAst">The type of <see cref="Ast" /> to expect in the list.</typeparam>
        /// <returns>The compiled <see cref="Expression" /> objects.</returns>
        public static Expression[] CompileAll<TAst>(this IList<TAst> asts, CompileVisitor visitor)
            where TAst : Ast
        {
            if (asts == null || asts.Count == 0)
            {
                return s_emptyExpressions;
            }

            var expressions = new Expression[asts.Count];
            for (var i = 0; i < asts.Count; i++)
            {
                expressions[i] = (Expression)asts[i].Visit(visitor);
            }

            return expressions;
        }

        /// <summary>
        /// Compile a <see cref="Ast" /> into a <see cref="Expression" />.
        /// </summary>
        /// <param name="ast">The <see cref="Ast" /> to compile.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the compile.</param>
        /// <returns>The compiled <see cref="Expression" /> object.</returns>
        public static Expression Compile(this Ast ast, CompileVisitor visitor)
        {
            try
            {
                return (Expression)ast.Visit(visitor);
            }
            catch (ArgumentException e)
            {
                visitor.Errors.ReportParseError(ast.Extent, e);
                return Expression.Empty();
            }
            catch (InvalidOperationException e)
            {
                visitor.Errors.ReportParseError(ast.Extent, e);
                return Expression.Empty();
            }
        }
    }
}
