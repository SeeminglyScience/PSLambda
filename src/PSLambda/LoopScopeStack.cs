using System;
using System.Linq.Expressions;

namespace PSLambda
{
    /// <summary>
    /// Represents a set of scopes in which the <c>break</c> or <c>continue</c> keywords
    /// may be used.
    /// </summary>
    internal class LoopScopeStack
    {
        private LoopScope _current;

        /// <summary>
        /// Gets the current label for the <c>break</c> keyword.
        /// </summary>
        internal LabelTarget Break => _current?.Break;

        /// <summary>
        /// Gets the current label for the <c>continue</c> keyword.
        /// </summary>
        internal LabelTarget Continue => _current?.Continue;

        /// <summary>
        /// Creates a new scope in which the <c>break</c> or <c>continue</c> keywords
        /// may be used.
        /// </summary>
        /// <returns>
        /// A <see cref="IDisposable" /> handle that will return to the previous scope when disposed.
        /// </returns>
        internal IDisposable NewScope()
        {
            _current = new LoopScope()
            {
                Parent = _current,
                Break = Expression.Label(),
                Continue = Expression.Label()
            };

            return new ScopeHandle(() => _current = _current?.Parent);
        }
    }
}
