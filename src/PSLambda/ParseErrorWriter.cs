using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides uniform writing and management of <see cref="ParseError" /> objects.
    /// </summary>
    internal abstract class ParseErrorWriter : IParseErrorWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParseErrorWriter" /> class.
        /// </summary>
        protected ParseErrorWriter()
        {
        }

        /// <summary>
        /// Creates an instance of the default <see cref="ParseErrorWriter" />. This
        /// error writer will report full error messages and will throw after a maximum
        /// of three errors.
        /// </summary>
        /// <returns>The defualt <see cref="ParseErrorWriter" />.</returns>
        public static ParseErrorWriter CreateDefault() => new DefaultParseErrorWriter();

        /// <summary>
        /// Creates an instance of the null <see cref="ParseErrorWriter" />. This
        /// error writer will not report any error messages and will throw immediately
        /// after the first error has been reported.
        /// </summary>
        /// <returns>The null <see cref="ParseErrorWriter" />.</returns>
        public static ParseErrorWriter CreateNull() => new NullParseErrorWriter();

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
        public virtual void ReportParseError(IScriptExtent extent, string id, string message)
        {
        }

        /// <summary>
        /// Throws if the error limit has been hit. Error limit may vary
        /// between implementations.
        /// </summary>
        public virtual void ThrowIfErrorLimitHit()
        {
        }

        /// <summary>
        /// Throws if any error has been reported.
        /// </summary>
        public virtual void ThrowIfAnyErrors()
        {
        }

        private class NullParseErrorWriter : ParseErrorWriter
        {
            private int _errorCount;

            public override void ReportParseError(IScriptExtent extent, string id, string message)
            {
                _errorCount++;
                throw new ParseException();
            }

            public override void ThrowIfAnyErrors()
            {
                if (_errorCount > 0)
                {
                    throw new ParseException();
                }
            }

            public override void ThrowIfErrorLimitHit()
            {
                if (_errorCount > 0)
                {
                    throw new ParseException();
                }
            }
        }

        private class DefaultParseErrorWriter : ParseErrorWriter
        {
            private const int ErrorLimit = 3;

            private readonly List<ParseError> _errors = new List<ParseError>();

            public override void ReportParseError(IScriptExtent extent, string id, string message)
            {
                _errors.Add(
                    new ParseError(
                        extent,
                        id,
                        message));

                ThrowIfErrorLimitHit();
            }

            public override void ThrowIfErrorLimitHit()
            {
                if (_errors.Count < ErrorLimit)
                {
                    return;
                }

                throw new ParseException(_errors.ToArray());
            }

            public override void ThrowIfAnyErrors()
            {
                if (_errors.Count < 1)
                {
                    return;
                }

                throw new ParseException(_errors.ToArray());
            }
        }
    }
}
