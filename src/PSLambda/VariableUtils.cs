using System;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides utility methods for variable expression operations.
    /// </summary>
    internal static class VariableUtils
    {
        /// <summary>
        /// Determines if the specified variable is referencing the automatic
        /// variable <c>$_</c> or <c>$PSItem</c>.
        /// </summary>
        /// <param name="variableExpressionAst">The variable expression to test.</param>
        /// <returns>
        /// <see langword="true" /> if the specified variable references
        /// dollar under, otherwise <see langword="false" />.
        /// </returns>
        public static bool IsDollarUnder(VariableExpressionAst variableExpressionAst)
            => IsDollarUnder(variableExpressionAst.VariablePath.UserPath);

        /// <summary>
        /// Determines if the specified variable is referencing the automatic
        /// variable <c>$_</c> or <c>$PSItem</c>.
        /// </summary>
        /// <param name="variableName">The variable name to test.</param>
        /// <returns>
        /// <see langword="true" /> if the specified variable references
        /// dollar under, otherwise <see langword="false" />.
        /// </returns>
        public static bool IsDollarUnder(string variableName)
        {
            return variableName.Equals("_", StringComparison.Ordinal)
                || variableName.Equals("PSItem", StringComparison.Ordinal);
        }
    }
}
