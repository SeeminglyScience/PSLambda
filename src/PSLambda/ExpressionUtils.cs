using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using static System.Linq.Expressions.Expression;

namespace PSLambda
{
    /// <summary>
    /// Provides utility methods for creating <see cref="Expression" /> objects for
    /// operations meant to mimic the PowerShell engine.
    /// </summary>
    internal static class ExpressionUtils
    {
        /// <summary>
        /// Creates an <see cref="Expression" /> representing a less than comparison operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSLessThan(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Equal(PSCompare(lhs, rhs, isCaseSensitive), Constant(-1));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a greater than comparison operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSGreaterThan(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Equal(PSCompare(lhs, rhs, isCaseSensitive), Constant(1));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a equals comparison operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSEquals(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Equal(PSCompare(lhs, rhs, isCaseSensitive), Constant(0));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a comparison operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSCompare(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Call(
                ReflectionCache.LanguagePrimitives_Compare,
                Convert(lhs, typeof(object)),
                Convert(rhs, typeof(object)),
                Constant(!isCaseSensitive, typeof(bool)));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a match comparision operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSMatch(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Call(
                ReflectionCache.Regex_IsMatch,
                PSConvertTo<string>(lhs),
                PSConvertTo<string>(rhs),
                Constant(
                    isCaseSensitive
                        ? RegexOptions.None
                        : RegexOptions.IgnoreCase,
                    typeof(RegexOptions)));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a like comparision operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSLike(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Call(
                New(
                    ReflectionCache.WildcardPattern_Ctor,
                    PSConvertTo<string>(rhs),
                    Constant(
                        isCaseSensitive ? WildcardOptions.None : WildcardOptions.IgnoreCase,
                        typeof(WildcardOptions))),
                ReflectionCache.WildcardPattern_IsMatch,
                PSConvertTo<string>(lhs));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a conversion operation using the
        /// PowerShell engine.
        /// </summary>
        /// <param name="target">The <see cref="Expression" /> to convert.</param>
        /// <param name="targetType">The <see cref="Type" /> to convert to.</param>
        /// <returns>An <see cref="Expression" /> representing the conversion.</returns>
        public static Expression PSConvertTo(Expression target, Type targetType)
        {
            return Convert(
                Call(
                    ReflectionCache.LanguagePrimitives_ConvertTo,
                    Convert(target, typeof(object)),
                    Constant(targetType, typeof(Type))),
                targetType);
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a conversion operation using the
        /// PowerShell engine.
        /// </summary>
        /// <param name="target">The <see cref="Expression" /> to convert.</param>
        /// <typeparam name="T">The <see cref="Type" /> to convert to.</typeparam>
        /// <returns>An <see cref="Expression" /> representing the conversion.</returns>
        public static Expression PSConvertTo<T>(Expression target)
        {
            return Call(
                ReflectionCache.LanguagePrimitives_ConvertToGeneric.MakeGenericMethod(new[] { typeof(T) }),
                Convert(target, typeof(object)));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a conversion operation using the
        /// PowerShell engine.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Expression" /> representing multiple expressions that need to be converted.
        /// </param>
        /// <typeparam name="T">The <see cref="Type" /> to convert to.</typeparam>
        /// <returns>An <see cref="Expression" /> representing the conversion.</returns>
        public static Expression PSConvertAllTo<T>(Expression source)
        {
            var enumeratorVar = Variable(typeof(IEnumerator));
            var collectionVar = Variable(typeof(List<T>));
            var breakLabel = Label();
            return Block(
                new[] { enumeratorVar, collectionVar },
                Assign(collectionVar, New(typeof(List<T>))),
                Assign(
                    enumeratorVar,
                    Call(
                        ReflectionCache.LanguagePrimitives_GetEnumerator,
                        source)),
                Loop(
                    IfThenElse(
                        Call(enumeratorVar, ReflectionCache.IEnumerator_MoveNext),
                        Call(
                            collectionVar,
                            Strings.AddMethodName,
                            Type.EmptyTypes,
                            PSConvertTo<T>(Call(enumeratorVar, ReflectionCache.IEnumerator_get_Current))),
                        Break(breakLabel)),
                    breakLabel),
                Call(collectionVar, Strings.ToArrayMethodName, Type.EmptyTypes));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of an
        /// <see cref="Expression" /> as being <c>true</c> using the rules of the PowerShell engine.
        /// </summary>
        /// <param name="eval">The <see cref="Expression" /> to evaluate.</param>
        /// <returns>An <see cref="Expression" /> representing the evaluation.</returns>
        public static Expression PSIsTrue(Expression eval)
        {
            return Call(
                ReflectionCache.LanguagePrimitives_IsTrue,
                Convert(eval, typeof(object)));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the conversion of a type using the
        /// "as" operator using the conversion rules of the PowerShell engine.
        /// </summary>
        /// <param name="eval">The <see cref="Expression" /> to convert.</param>
        /// <param name="expectedType">The <see cref="Type" /> to convert to.</param>
        /// <returns>An <see cref="Expression" /> representing the conversion.</returns>
        public static Expression PSTypeAs(Expression eval, Type expectedType)
        {
            var resultVar = Variable(expectedType);
            return Block(
                expectedType,
                new[] { resultVar },
                Call(
                    ReflectionCache.LangaugePrimitives_TryConvertToGeneric.MakeGenericMethod(expectedType),
                    Convert(eval, typeof(object)),
                    resultVar),
                resultVar);
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of the "join" operator
        /// from the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSJoin(Expression lhs, Expression rhs)
        {
            return Call(
                ReflectionCache.String_Join,
                PSConvertTo<string>(rhs),
                PSConvertAllTo<string>(lhs));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of the "replace" operator
        /// from the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSReplace(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Call(
                ReflectionCache.Regex_Replace,
                PSConvertTo<string>(lhs),
                PSConvertTo<string>(ArrayIndex(rhs, Constant(0))),
                PSConvertTo<string>(ArrayIndex(rhs, Constant(1))),
                Constant(isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of the "split" operator
        /// from the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSSplit(Expression lhs, Expression rhs, bool isCaseSensitive)
        {
            return Call(
                ReflectionCache.Regex_Split,
                PSConvertTo<string>(lhs),
                PSConvertTo<string>(rhs),
                Constant(isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing a in comparison operator in
        /// the PowerShell engine.
        /// </summary>
        /// <param name="item">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="items">The <see cref="Expression" /> on the right hand side.</param>
        /// <param name="isCaseSensitive">
        /// A value indicating whether the operation should be case sensitive.
        /// </param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSIsIn(Expression item, Expression items, bool isCaseSensitive)
        {
            var returnLabel = Label(typeof(bool));
            var enumeratorVar = Variable(typeof(IEnumerator));
            return Block(
                new[] { enumeratorVar },
                Assign(
                    enumeratorVar,
                    Call(
                        ReflectionCache.LanguagePrimitives_GetEnumerator,
                        items)),
                Loop(
                    IfThenElse(
                        Call(enumeratorVar, ReflectionCache.IEnumerator_MoveNext),
                        IfThen(
                            PSEquals(
                                item,
                                Call(enumeratorVar, ReflectionCache.IEnumerator_get_Current),
                                isCaseSensitive),
                            Return(returnLabel, SpecialVariables.Constants[Strings.TrueVariableName])),
                        Return(returnLabel, SpecialVariables.Constants[Strings.FalseVariableName]))),
                Label(returnLabel, SpecialVariables.Constants[Strings.FalseVariableName]));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of the "format" operator
        /// from the PowerShell engine.
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSFormat(Expression lhs, Expression rhs)
        {
            if (rhs.NodeType == ExpressionType.NewArrayInit)
            {
                return Call(
                    ReflectionCache.String_Format,
                    Property(null, ReflectionCache.CultureInfo_CurrentCulture),
                    PSConvertTo<string>(lhs),
                    PSConvertAllTo<string>(rhs));
            }

            return Call(
                ReflectionCache.String_Format,
                Property(null, ReflectionCache.CultureInfo_CurrentCulture),
                PSConvertTo<string>(lhs),
                NewArrayInit(typeof(object), PSConvertTo<string>(rhs)));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of the "DotDot" operator
        /// from the PowerShell engine (e.g. 0..100).
        /// </summary>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSDotDot(Expression lhs, Expression rhs)
        {
            return Call(
                ReflectionCache.ExpressionUtils_GetRange,
                PSConvertTo<int>(lhs),
                PSConvertTo<int>(rhs));
        }

        /// <summary>
        /// Creates an <see cref="Expression" /> representing the evaluation of a bitwise comparision
        /// operator from the PowerShell engine.
        /// </summary>
        /// <param name="expressionType">The expression operator.</param>
        /// <param name="lhs">The <see cref="Expression" /> on the left hand side.</param>
        /// <param name="rhs">The <see cref="Expression" /> on the right hand side.</param>
        /// <returns>An <see cref="Expression" /> representing the operation.</returns>
        public static Expression PSBitwiseOperation(
            ExpressionType expressionType,
            Expression lhs,
            Expression rhs)
        {
            var resultType = lhs.Type;
            if (typeof(Enum).IsAssignableFrom(lhs.Type))
            {
                lhs = Convert(lhs, Enum.GetUnderlyingType(lhs.Type));
            }

            if (typeof(Enum).IsAssignableFrom(rhs.Type))
            {
                rhs = Convert(rhs, Enum.GetUnderlyingType(rhs.Type));
            }

            var resultExpression = MakeBinary(expressionType, lhs, rhs);
            if (resultType == resultExpression.Type)
            {
                return resultExpression;
            }

            return PSConvertTo(resultExpression, resultType);
        }

        private static bool PSEqualsIgnoreCase(object first, object second)
        {
            return LanguagePrimitives.Compare(first, second, ignoreCase: true) == 0;
        }

        private static int[] GetRange(int start, int end)
        {
            bool shouldReverse = false;
            if (start > end)
            {
                var tempStart = start;
                start = end;
                end = tempStart;
                shouldReverse = true;
            }

            var result = new int[end - start + 1];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = i + start;
            }

            if (!shouldReverse)
            {
                return result;
            }

            for (var i = 0; i < result.Length / 2; i++)
            {
                var temp = result[i];
                result[i] = result[result.Length - 1 - i];
                result[result.Length - 1 - i] = temp;
            }

            return result;
        }
    }
}
