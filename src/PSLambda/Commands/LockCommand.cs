using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides handling for the "lock" custom command.
    /// </summary>
    internal class LockCommand : ObjectAndBodyCommandHandler
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public override string CommandName { get; } = "lock";

        /// <summary>
        /// Creates a Linq expression for a <see cref="CommandAst" /> representing
        /// the "lock" command.
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
            var lockVar = Expression.Variable(typeof(object));
            var lockTakenVar = Expression.Variable(typeof(bool));
            return visitor.NewBlock(() =>
                Expression.Block(
                    typeof(void),
                    new[] { lockVar, lockTakenVar },
                    Expression.Call(
                        ReflectionCache.Monitor_Enter,
                        Expression.Assign(
                            lockVar,
                            Expression.Convert(
                                targetAst.Compile(visitor),
                                typeof(object))),
                        lockTakenVar),
                    Expression.TryFinally(
                        bodyAst.ScriptBlock.EndBlock.Compile(visitor),
                        Expression.IfThen(
                            lockTakenVar,
                            Expression.Call(
                                ReflectionCache.Monitor_Exit,
                                lockVar)))));
        }
    }
}
