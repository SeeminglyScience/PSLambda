using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides utility methods for empty elements.
    /// </summary>
    internal static class Empty
    {
        /// <summary>
        /// Gets an empty <see cref="IScriptExtent" />.
        /// </summary>
        internal static IScriptExtent Extent => new EmptyScriptExtent();

        /// <summary>
        /// Gets an empty <see cref="IScriptPosition" />.
        /// </summary>
        internal static IScriptPosition Position => new EmptyScriptPosition();

        private class EmptyScriptExtent : IScriptExtent
        {
            public int EndColumnNumber => 0;

            public int EndLineNumber => 0;

            public int EndOffset => 0;

            public IScriptPosition EndScriptPosition => Position;

            public string File => string.Empty;

            public int StartColumnNumber => 0;

            public int StartLineNumber => 0;

            public int StartOffset => 0;

            public IScriptPosition StartScriptPosition => Position;

            public string Text => string.Empty;
        }

        private class EmptyScriptPosition : IScriptPosition
        {
            public int ColumnNumber => 0;

            public string File => string.Empty;

            public string Line => string.Empty;

            public int LineNumber => 0;

            public int Offset => 0;

            public string GetFullScript() => string.Empty;
        }
    }
}
