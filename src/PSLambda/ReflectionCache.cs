using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PSLambda
{
    /// <summary>
    /// Provides cached objects for generating method and property invocation
    /// <see cref="System.Linq.Expressions.Expression" /> objects.
    /// </summary>
    internal static class ReflectionCache
    {
        /// <summary>
        /// Resolves to <see cref="LanguagePrimitives.GetEnumerator(object)" />.
        /// </summary>
        public static readonly MethodInfo LanguagePrimitives_GetEnumerator =
            typeof(LanguagePrimitives).GetMethod("GetEnumerator", new[] { typeof(object) });

        /// <summary>
        /// Resolves to <see cref="LanguagePrimitives.IsTrue(object)" />.
        /// </summary>
        public static readonly MethodInfo LanguagePrimitives_IsTrue =
            typeof(LanguagePrimitives).GetMethod("IsTrue", new[] { typeof(object) });

        /// <summary>
        /// Resolves to <see cref="LanguagePrimitives.Compare(object, object, bool)" />.
        /// </summary>
        public static readonly MethodInfo LanguagePrimitives_Compare =
            typeof(LanguagePrimitives).GetMethod("Compare", new[] { typeof(object), typeof(object), typeof(bool) });

        /// <summary>
        /// Resolves to <see cref="LanguagePrimitives.ConvertTo{T}(object)" />.
        /// </summary>
        public static readonly MethodInfo LanguagePrimitives_ConvertToGeneric =
            typeof(LanguagePrimitives).GetMethod("ConvertTo", new[] { typeof(object) });

        /// <summary>
        /// Resolves to <see cref="LanguagePrimitives.ConvertTo(object, Type)" />.
        /// </summary>
        public static readonly MethodInfo LanguagePrimitives_ConvertTo =
            typeof(LanguagePrimitives).GetMethod("ConvertTo", new[] { typeof(object), typeof(Type) });

        /// <summary>
        /// Resolves to <see cref="LanguagePrimitives.TryConvertTo{T}(object, out T)" />.
        /// </summary>
        public static readonly MethodInfo LangaugePrimitives_TryConvertToGeneric =
            (MethodInfo)typeof(LanguagePrimitives).FindMembers(
                MemberTypes.Method,
                BindingFlags.Static | BindingFlags.Public,
                (m, filterCriteria) =>
                {
                    var method = m as MethodInfo;
                    if (!method.IsGenericMethod)
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                        && parameters[0].ParameterType == typeof(object)
                        && parameters[1].ParameterType.IsByRef;
                },
                null)
                .FirstOrDefault();

        /// <summary>
        /// Resolves to <see cref="WildcardPattern.WildcardPattern(string, WildcardOptions)" />.
        /// </summary>
        public static readonly ConstructorInfo WildcardPattern_Ctor =
            typeof(WildcardPattern).GetConstructor(new[] { typeof(string), typeof(WildcardOptions) });

        /// <summary>
        /// Resolves to <see cref="Regex.IsMatch(string, string, RegexOptions)" />.
        /// </summary>
        public static readonly MethodInfo Regex_IsMatch =
            typeof(Regex).GetMethod("IsMatch", new[] { typeof(string), typeof(string), typeof(RegexOptions) });

        /// <summary>
        /// Resolves to <see cref="Regex.Replace(string, string, string, RegexOptions)" />.
        /// </summary>
        public static readonly MethodInfo Regex_Replace =
            typeof(Regex).GetMethod("Replace", new[] { typeof(string), typeof(string), typeof(string), typeof(RegexOptions) });

        /// <summary>
        /// Resolves to <see cref="Regex.Split(string, string, RegexOptions)" />.
        /// </summary>
        public static readonly MethodInfo Regex_Split =
            typeof(Regex).GetMethod("Split", new[] { typeof(string), typeof(string), typeof(RegexOptions) });

        /// <summary>
        /// Resolves to <see cref="WildcardPattern.IsMatch(string)" />.
        /// </summary>
        public static readonly MethodInfo WildcardPattern_IsMatch =
            typeof(WildcardPattern).GetMethod("IsMatch", new[] { typeof(string) });

        /// <summary>
        /// Resolves to <see cref="IEnumerator.MoveNext" />.
        /// </summary>
        public static readonly MethodInfo IEnumerator_MoveNext =
            typeof(IEnumerator).GetMethod("MoveNext");

        /// <summary>
        /// Resolves to <see cref="string.Join(string, string[])" />.
        /// </summary>
        public static readonly MethodInfo String_Join =
            typeof(string).GetMethod("Join", new[] { typeof(string), typeof(string[]) });

        /// <summary>
        /// Resolves to <see cref="string.Format(IFormatProvider, string, object[])" />.
        /// </summary>
        public static readonly MethodInfo String_Format =
            typeof(string).GetMethod("Format", new[] { typeof(IFormatProvider), typeof(string), typeof(object[]) });

        /// <summary>
        /// Resolves to <see cref="System.Globalization.CultureInfo.CurrentCulture" />.
        /// </summary>
        public static readonly PropertyInfo CultureInfo_CurrentCulture =
            typeof(System.Globalization.CultureInfo).GetProperty("CurrentCulture");

        /// <summary>
        /// Resolves to the non-public type System.Management.Automation.ArgumentTypeConverterAttribute.
        /// </summary>
        public static readonly Type ArgumentTypeConverterAttribute =
            typeof(PSObject).Assembly.GetType("System.Management.Automation.ArgumentTypeConverterAttribute");

        /// <summary>
        /// Resolves to the non-public property System.Management.Automation.ArgumentTypeConverterAttribute.TargetType.
        /// </summary>
        public static readonly PropertyInfo ArgumentTypeConverterAttribute_TargetType =
            ArgumentTypeConverterAttribute?.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Resolves to <see cref="RuntimeException.RuntimeException(string)" />.
        /// </summary>
        public static readonly ConstructorInfo RuntimeException_Ctor =
            typeof(RuntimeException).GetConstructor(new[] { typeof(string) });

        /// <summary>
        /// Resolves to <see cref="IDisposable.Dispose" />.
        /// </summary>
        public static readonly MethodInfo IDisposable_Dispose =
            typeof(IDisposable).GetMethod("Dispose");

        /// <summary>
        /// Resolves to the indexer for <see cref="System.Collections.IList" />.
        /// </summary>
        public static readonly PropertyInfo IList_Item =
            typeof(IList).GetProperty("Item");

        /// <summary>
        /// Resolves to the indexer for <see cref="System.Collections.IDictionary" />.
        /// </summary>
        public static readonly PropertyInfo IDictionary_Item =
            typeof(IDictionary).GetProperty("Item");

        /// <summary>
        /// Resolves to the non-public parameterless constructor for System.Management.Automation.ExitException.
        /// </summary>
        public static readonly ConstructorInfo ExitException_Ctor =
            typeof(ExitException).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[0],
                new ParameterModifier[0]);

        /// <summary>
        /// Resolves to the non-public constructor System.Management.Automation.ExitException(Object).
        /// </summary>
        public static readonly ConstructorInfo ExitException_Ctor_Object =
            typeof(ExitException).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                new[] { new ParameterModifier(1) });

        /// <summary>
        /// Resolves to <see cref="Hashtable.Add(object, object)" />.
        /// </summary>
        public static readonly MethodInfo Hashtable_Add =
            typeof(Hashtable).GetMethod("Add", new[] { typeof(object), typeof(object) });

        /// <summary>
        /// Resolves to <see cref="Hashtable.Hashtable(int, IEqualityComparer)" />
        /// </summary>
        public static readonly ConstructorInfo Hashtable_Ctor =
            typeof(Hashtable).GetConstructor(new[] { typeof(int), typeof(IEqualityComparer) });

        /// <summary>
        /// Resolves to <see cref="IEnumerator.Current" />
        /// </summary>
        public static readonly PropertyInfo IEnumerator_Current =
            typeof(IEnumerator).GetProperty("Current");

        /// <summary>
        /// Resolves to <see cref="StringComparer.CurrentCultureIgnoreCase" />.
        /// </summary>
        public static readonly PropertyInfo StringComparer_CurrentCultureIgnoreCase =
            typeof(StringComparer).GetProperty("CurrentCultureIgnoreCase");

        /// <summary>
        /// Resolves to <see cref="ExpressionUtils.PSEqualsIgnoreCase(object, object)" />.
        /// </summary>
        public static readonly MethodInfo ExpressionUtils_PSEqualsIgnoreCase =
            typeof(ExpressionUtils).GetMethod("PSEqualsIgnoreCase", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Resolves to <see cref="ExpressionUtils.GetRange(int, int)" />.
        /// </summary>
        public static readonly MethodInfo ExpressionUtils_GetRange =
            typeof(ExpressionUtils).GetMethod("GetRange", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Resolves to <see cref="System.Threading.Monitor.Enter(object, ref bool)" />.
        /// </summary>
        public static readonly MethodInfo Monitor_Enter =
            typeof(System.Threading.Monitor).GetMethod(
                "Enter",
                new[] { typeof(object), typeof(bool).MakeByRefType() });

        /// <summary>
        /// Resolves to <see cref="System.Threading.Monitor.Exit(object)" />.
        /// </summary>
        public static readonly MethodInfo Monitor_Exit =
            typeof(System.Threading.Monitor).GetMethod("Exit", new[] { typeof(object) });
    }
}
