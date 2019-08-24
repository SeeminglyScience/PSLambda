using System;
using System.Globalization;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides utility methods for reporting specific types of
    /// <see cref="ParseError" /> objects.
    /// </summary>
    internal static class ParseWriterExtensions
    {
        /// <summary>
        /// Generate a <see cref="ParseError" /> citing an unsupported <see cref="Ast" /> type.
        /// </summary>
        /// <param name="writer">The <see cref="IParseErrorWriter" /> that should issue the report.</param>
        /// <param name="ast">The <see cref="Ast" /> that is not supported.</param>
        internal static void ReportNotSupportedAstType(
            this IParseErrorWriter writer,
            Ast ast)
        {
            writer.ReportNotSupported(
                ast.Extent,
                ast.GetType().Name,
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.AstNotSupported,
                    ast.GetType().Name));
        }

        /// <summary>
        /// Generate a <see cref="ParseError" /> citing an unsupported element.
        /// </summary>
        /// <param name="writer">The <see cref="IParseErrorWriter" /> that should issue the report.</param>
        /// <param name="extent">The <see cref="IScriptExtent" /> of the unsupported element.</param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        /// <param name="message">The message to be shown in the <see cref="ParseError" />.</param>
        internal static void ReportNotSupported(
            this IParseErrorWriter writer,
            IScriptExtent extent,
            string id,
            string message)
        {
            writer.ReportParseError(
                extent,
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}.{1}",
                    nameof(ErrorStrings.ElementNotSupported),
                    id),
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.ElementNotSupported,
                    message));
        }

        /// <summary>
        /// Generate a <see cref="ParseError" /> citing a missing element.
        /// </summary>
        /// <param name="writer">The <see cref="IParseErrorWriter" /> that should issue the report.</param>
        /// <param name="extent">The <see cref="IScriptExtent" /> of the missing element.</param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        /// <param name="message">The message to be shown in the <see cref="ParseError" />.</param>
        internal static void ReportMissing(
            this IParseErrorWriter writer,
            IScriptExtent extent,
            string id,
            string message)
        {
            writer.ReportParseError(
                extent,
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}.{1}",
                    nameof(ErrorStrings.ElementMissing),
                    id),
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.ElementMissing,
                    message));
        }

        /// <summary>
        /// Generate a <see cref="ParseError" />.
        /// </summary>
        /// <param name="writer">The <see cref="IParseErrorWriter" /> that should issue the report.</param>
        /// <param name="extent">The <see cref="IScriptExtent" /> of element in error.</param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        /// <param name="message">The message to be shown in the <see cref="ParseError" />.</param>
        internal static void ReportParseError(
            this IParseErrorWriter writer,
            IScriptExtent extent,
            string id = nameof(ErrorStrings.CompileTimeParseError),
            string message = "")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = ErrorStrings.CompileTimeParseError;
            }

            writer.ReportParseError(extent, id, message);
        }

        /// <summary>
        /// Generate a <see cref="ParseError" />.
        /// </summary>
        /// <param name="writer">The <see cref="IParseErrorWriter" /> that should issue the report.</param>
        /// <param name="extent">The <see cref="IScriptExtent" /> of element in error.</param>
        /// <param name="exception">
        /// The exception housing the message to be shown in the <see cref="ParseError" />.
        /// </param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        internal static void ReportParseError(
            this IParseErrorWriter writer,
            IScriptExtent extent,
            Exception exception,
            string id = "")
        {
            if (string.IsNullOrEmpty(id))
            {
                id = exception.GetType().Name;
            }

            writer.ReportParseError(
                extent,
                id,
                exception.Message);
        }

        /// <summary>
        /// Generate a <see cref="ParseError" />.
        /// </summary>
        /// <param name="writer">The <see cref="IParseErrorWriter" /> that should issue the report.</param>
        /// <param name="extent">The <see cref="IScriptExtent" /> of element in error.</param>
        internal static void ReportNonConstantTypeAs(
            this IParseErrorWriter writer,
            IScriptExtent extent)
        {
            writer.ReportNotSupported(
                extent,
                nameof(ErrorStrings.NonConstantTypeAs),
                ErrorStrings.NonConstantTypeAs);
        }
    }
}
