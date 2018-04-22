using System.Linq.Expressions;

namespace PSLambda
{
    /// <summary>
    /// Represents a scope in which the <c>break</c> or <c>continue</c> keywords
    /// may be used.
    /// </summary>
    internal class LoopScope
    {
        /// <summary>
        /// Gets or sets the parent scope.
        /// </summary>
        public LoopScope Parent { get; set; }

        /// <summary>
        /// Gets or sets the label for the <c>break</c> keyword.
        /// </summary>
        public LabelTarget Break { get; set; }

        /// <summary>
        /// Gets or sets the label for the <c>continue</c> keyword.
        /// </summary>
        public LabelTarget Continue { get; set; }
    }
}
