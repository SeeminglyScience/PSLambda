using System.Management.Automation;

namespace PSLambda
{
    /// <summary>
    /// Provides a strongly typed wrapper for <see cref="PSVariable" /> objects.
    /// </summary>
    /// <typeparam name="TValue">The type of object contained by the <see cref="PSVariable" />.</typeparam>
    internal class PSVariableWrapper<TValue>
    {
        private readonly object _syncObject = new object();

        private readonly PSVariable _wrappedVariable;

        /// <summary>
        /// Initializes a new instance of the <see cref="PSVariableWrapper{TValue}" /> class.
        /// </summary>
        /// <param name="variable">The <see cref="PSVariable" /> to wrap.</param>
        public PSVariableWrapper(PSVariable variable)
        {
            _wrappedVariable = variable;
        }

        /// <summary>
        /// Gets or sets the value of the <see cref="PSVariable" /> object.
        /// </summary>
        public TValue Value
        {
            get
            {
                lock (_syncObject)
                {
                    return LanguagePrimitives.ConvertTo<TValue>(_wrappedVariable.Value);
                }
            }

            set
            {
                lock (_syncObject)
                {
                    _wrappedVariable.Value = value;
                }
            }
        }
    }
}
