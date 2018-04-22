using System;
using System.Linq.Expressions;

namespace PSLambda
{
    /// <summary>
    /// Represents the a scope in which the <c>return</c> keyword will work.
    /// </summary>
    internal class LabelScope
    {
        /// <summary>
        /// The parent scope.
        /// </summary>
        internal readonly LabelScope _parent;

        private LabelTarget _label;

        /// <summary>
        /// Initializes a new instance of the <see cref="LabelScope" /> class.
        /// </summary>
        internal LabelScope()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LabelScope" /> class.
        /// </summary>
        /// <param name="parent">The parent <see cref="LabelScope" />.</param>
        internal LabelScope(LabelScope parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Gets or sets a value indicating whether an explicit return statement has been used.
        /// </summary>
        public bool IsReturnRequested { get; set; }

        /// <summary>
        /// Gets or sets the implied or explicit return type.
        /// </summary>
        public Type ReturnType { get; set; }

        /// <summary>
        /// Gets the current <c>return</c> label.
        /// </summary>
        public LabelTarget Label
        {
            get
            {
                if (_label != null)
                {
                    return _label;
                }

                _label = Expression.Label(ReturnType);
                return _label;
            }
        }
    }
}
