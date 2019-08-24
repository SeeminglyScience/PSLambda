using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides conversion from <see cref="PSDelegate" /> to any <see cref="Delegate" /> type.
    /// </summary>
    public class DelegateTypeConverter : PSTypeConverter
    {
        private static readonly Dictionary<PSDelegate, Dictionary<Type, Delegate>> s_delegateCache = new Dictionary<PSDelegate, Dictionary<Type, Delegate>>();

        private static readonly object s_syncObject = new object();

        /// <summary>
        /// Determines if a object can be converted to a specific type.
        /// </summary>
        /// <param name="sourceValue">The value to convert.</param>
        /// <param name="destinationType">The type to conver to.</param>
        /// <returns>A value indicating whether the object can be converted.</returns>
        public override bool CanConvertFrom(object sourceValue, Type destinationType)
        {
            return sourceValue is PSDelegate && typeof(Delegate).IsAssignableFrom(destinationType);
        }

        /// <summary>
        /// Determines if a object can be converted to a specific type.
        /// </summary>
        /// <param name="sourceValue">The value to convert.</param>
        /// <param name="destinationType">The type to conver to.</param>
        /// <returns>A value indicating whether the object can be converted.</returns>
        public override bool CanConvertTo(object sourceValue, Type destinationType)
        {
            return sourceValue is PSDelegate && typeof(Delegate).IsAssignableFrom(destinationType);
        }

        /// <summary>
        /// Converts a <see cref="PSDelegate" /> object to a <see cref="Delegate" /> type.
        /// </summary>
        /// <param name="sourceValue">The <see cref="PSDelegate" /> to convert.</param>
        /// <param name="destinationType">
        /// The type inheriting from <see cref="Delegate" /> to convert to.
        /// </param>
        /// <param name="formatProvider">The parameter is not used.</param>
        /// <param name="ignoreCase">The parameter is not used.</param>
        /// <returns>The converted <see cref="Delegate" /> object.</returns>
        public override object ConvertFrom(object sourceValue, Type destinationType, IFormatProvider formatProvider, bool ignoreCase)
        {
            return ConvertToDelegate((PSDelegate)sourceValue, destinationType);
        }

        /// <summary>
        /// Converts a <see cref="PSDelegate" /> object to a <see cref="Delegate" /> type.
        /// </summary>
        /// <param name="sourceValue">The <see cref="PSDelegate" /> to convert.</param>
        /// <param name="destinationType">
        /// The type inheriting from <see cref="Delegate" /> to convert to.
        /// </param>
        /// <param name="formatProvider">The parameter is not used.</param>
        /// <param name="ignoreCase">The parameter is not used.</param>
        /// <returns>The converted <see cref="Delegate" /> object.</returns>
        public override object ConvertTo(object sourceValue, Type destinationType, IFormatProvider formatProvider, bool ignoreCase)
        {
            return ConvertToDelegate((PSDelegate)sourceValue, destinationType);
        }

        private Delegate ConvertToDelegate(PSDelegate psDelegate, Type destinationType)
        {
            lock (s_syncObject)
            {
                if (!s_delegateCache.TryGetValue(psDelegate, out Dictionary<Type, Delegate> cacheEntry))
                {
                    cacheEntry = new Dictionary<Type, Delegate>();
                    s_delegateCache.Add(psDelegate, cacheEntry);
                }

                if (!cacheEntry.TryGetValue(destinationType, out Delegate compiledDelegate))
                {
                    compiledDelegate = CompileVisitor.CompileAst(
                        psDelegate.EngineIntrinsics,
                        (ScriptBlockAst)psDelegate.ScriptBlock.Ast,
                        psDelegate.Locals.Values.ToArray(),
                        destinationType);

                    cacheEntry.Add(destinationType, compiledDelegate);
                }

                return compiledDelegate;
            }
        }
    }
}
