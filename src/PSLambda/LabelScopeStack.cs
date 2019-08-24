using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace PSLambda
{
    /// <summary>
    /// Represents the current set of scopes in which the <c>return</c> keyword will work.
    /// </summary>
    internal class LabelScopeStack
    {
        private LabelScope _current;

        /// <summary>
        /// Gets the implied or explicit return type.
        /// </summary>
        public Type ReturnType => _current?.ReturnType;

        /// <summary>
        /// Creates a new scope in which the <c>return</c> keyword will work.
        /// </summary>
        /// <returns>
        /// A <see cref="IDisposable" /> handle that will return to the previous scope when disposed.
        /// </returns>
        public IDisposable NewScope()
        {
            _current = _current == null ? new LabelScope() : new LabelScope(_current);
            return new ScopeHandle(() => _current = _current?._parent);
        }

        /// <summary>
        /// Creates a new scope in which the <c>return</c> keyword will work while specifying an
        /// expected return type.
        /// </summary>
        /// <param name="returnType">The expected return type.</param>
        /// <returns>
        /// A <see cref="IDisposable" /> handle that will return to the previous scope when disposed.
        /// </returns>
        public IDisposable NewScope(Type returnType)
        {
            _current = _current == null ? new LabelScope() : new LabelScope(_current);
            _current.ReturnType = returnType;
            return new ScopeHandle(() => _current = _current?._parent);
        }

        /// <summary>
        /// Gets the specified statement adding the <c>return</c> label if applicable.
        /// </summary>
        /// <param name="expressions">The expressions to precent the label.</param>
        /// <param name="requireExplicitReturn">
        /// A value indicating whether an explicit return statement should be required.
        /// </param>
        /// <returns>
        /// If a <c>return</c> label is required the supplied expressions will be returned followed
        /// by the required label, otherwise they will be returned unchanged.
        /// </returns>
        public Expression[] WithReturn(IEnumerable<Expression> expressions, bool requireExplicitReturn = true)
        {
            if (_current.IsReturnRequested)
            {
                return expressions.Concat(
                    new[]
                    {
                        Expression.Label(
                            _current.Label,
                            Expression.Default(_current.ReturnType)),
                    }).ToArray();
            }

            if (!requireExplicitReturn)
            {
                return expressions.ToArray();
            }

            _current.IsReturnRequested = true;
            _current.ReturnType = typeof(void);
            return expressions.Concat(new[] { Expression.Label(_current.Label) }).ToArray();
        }

        /// <summary>
        /// Gets an existing <c>return</c> label if one has already been defined, otherwise
        /// one is created.
        /// </summary>
        /// <param name="type">The expected return type.</param>
        /// <returns>The <see cref="LabelTarget" /> requested.</returns>
        public LabelTarget GetOrCreateReturnLabel(Type type)
        {
            if (_current != null && _current.IsReturnRequested)
            {
                return _current.Label;
            }

            _current.IsReturnRequested = true;
            _current.ReturnType = type;
            return _current.Label;
        }
    }
}
