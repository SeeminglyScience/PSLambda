using System;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides handling for the "with" custom command.
    /// </summary>
    internal class WithCommand : ObjectAndBodyCommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public override string CommandName { get; } = "with";

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// the "with" command.
        /// </summary>
        /// <param name="commandAst">The AST to convert.</param>
        /// <param name="targetAst">The AST containing the target of the keyword.</param>
        /// <param name="bodyAst">The AST containing the body of the keyword.</param>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the expression.</param>
        /// <returns>An expression representing the command.</returns>
        protected override Expression ProcessObjectAndBody(
            CommandAst commandAst,
            CommandElementAst targetAst,
            ScriptBlockExpressionAst bodyAst,
            CompileVisitor visitor)
        {
            var disposeVar = Expression.Variable(typeof(IDisposable));
            return visitor.NewBlock(() =>
                Expression.Block(
                    typeof(void),
                    new[] { disposeVar },
                    Expression.Assign(
                        disposeVar,
                        Expression.Convert(
                            targetAst.Compile(visitor),
                            typeof(IDisposable))),
                    Expression.TryFinally(
                        bodyAst.ScriptBlock.EndBlock.Compile(visitor),
                        Expression.Call(disposeVar, ReflectionCache.IDisposable_Dispose))));
        }
    }
}
