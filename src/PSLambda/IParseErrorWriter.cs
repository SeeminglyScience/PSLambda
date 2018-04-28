using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides uniform writing and management of <see cref="ParseError" /> objects.
    /// </summary>
    internal interface IParseErrorWriter
    {
        /// <summary>
        /// Reports a <see cref="ParseError" />. Handling of the <see cref="ParseError" />
        /// may defer between implementations.
        /// </summary>
        /// <param name="extent">
        /// The <see cref="IScriptExtent" /> of the error being reported.
        /// </param>
        /// <param name="id">The id of the error.</param>
        /// <param name="message">
        /// The message to display in the <see cref="System.Management.Automation.ParseException" />
        /// when parsing is completed.
        /// </param>
        void ReportParseError(
            IScriptExtent extent,
            string id,
            string message);

        /// <summary>
        /// Throws if the error limit has been hit. Error limit may vary
        /// between implementations.
        /// </summary>
        void ThrowIfErrorLimitHit();

        /// <summary>
        /// Throws if any error has been reported.
        /// </summary>
        void ThrowIfAnyErrors();
    }
}
