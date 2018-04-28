using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PSLambda
{
    /// <summary>
    /// Provides member binding for method invocation expressions.
    /// </summary>
    internal class MemberBinder
    {
        private static readonly PSVariable[] s_emptyVariables = new PSVariable[0];

        private static readonly ParameterAst[] s_emptyParameterAsts = new ParameterAst[0];

        private readonly BindingFlags _instanceFlags;

        private readonly BindingFlags _staticFlags;

        private readonly string[] _namespaces;

        private MethodInfo[] _extensionMethods;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberBinder" /> class.
        /// </summary>
        /// <param name="accessFlags">
        /// The <see cref="BindingFlags" /> to use while resolving members. Only pass
        /// access modifiers (e.g. <see cref="BindingFlags.Public" /> or
        /// <see cref="BindingFlags.NonPublic" />.</param>
        /// <param name="namespaces">The namespaces to resolve extension methods in.</param>
        public MemberBinder(BindingFlags accessFlags, string[] namespaces)
        {
            _instanceFlags = accessFlags | BindingFlags.IgnoreCase | BindingFlags.Instance;
            _staticFlags = accessFlags | BindingFlags.IgnoreCase | BindingFlags.Static;
            _namespaces = new string[namespaces.Length];
            namespaces.CopyTo(_namespaces, 0);
        }

        /// <summary>
        /// Determines the correct member for an expression and creates a
        /// <see cref="Expression" /> representing it's invocation.
        /// </summary>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the bind.</param>
        /// <param name="instance">The instance <see cref="Expression" /> for the invocation.</param>
        /// <param name="name">The member name to use while resolving the method.</param>
        /// <param name="arguments">The arguments to use while resolving the method.</param>
        /// <param name="genericArguments">The generic arguments to use while resolving the method.</param>
        /// <returns>
        /// A <see cref="BindingResult" /> that either contains the
        /// <see cref="Expression" /> or the <see cref="ArgumentException" /> and
        /// error ID.
        /// </returns>
        internal BindingResult BindMethod(
            CompileVisitor visitor,
            Expression instance,
            string name,
            Ast[] arguments,
            Type[] genericArguments)
        {
            return BindMethod(
                visitor,
                instance.Type,
                name,
                arguments,
                instance,
                genericArguments);
        }

        /// <summary>
        /// Determines the correct member for an expression and creates a
        /// <see cref="Expression" /> representing it's invocation.
        /// </summary>
        /// <param name="visitor">The <see cref="CompileVisitor" /> requesting the bind.</param>
        /// <param name="sourceType">The source <see cref="Type" /> for the invocation.</param>
        /// <param name="name">The member name to use while resolving the method.</param>
        /// <param name="arguments">The arguments to use while resolving the method.</param>
        /// <param name="genericArguments">The generic arguments to use while resolving the method.</param>
        /// <returns>
        /// A <see cref="BindingResult" /> that either contains the
        /// <see cref="Expression" /> or the <see cref="ArgumentException" /> and
        /// error ID.
        /// </returns>
        internal BindingResult BindMethod(
            CompileVisitor visitor,
            Type sourceType,
            string name,
            Ast[] arguments,
            Type[] genericArguments)
        {
            return BindMethod(
                visitor,
                sourceType,
                name,
                arguments,
                null,
                genericArguments);
        }

        private BindingResult BindMethod(
            CompileVisitor visitor,
            Type sourceType,
            string name,
            Ast[] arguments,
            Expression instance,
            Type[] genericArguments)
        {
            var methodArgs = new MethodArgument[arguments.Length];
            for (var i = 0; i < methodArgs.Length; i++)
            {
                methodArgs[i] = (MethodArgument)arguments[i];
            }

            var didFindName = false;
            var isInstance = instance != null;
            var methods = sourceType.GetMethods(isInstance ? _instanceFlags : _staticFlags);
            MethodInfo boundMethod;
            BindingResult bindingResult = default(BindingResult);
            for (var i = 0; i < methods.Length; i++)
            {
                if (!methods[i].Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (!didFindName)
                {
                    didFindName = true;
                }

                if (genericArguments.Length > 0 &&
                    (!methods[i].IsGenericMethod ||
                    !AreGenericArgumentsValid(methods[i], genericArguments)))
                {
                        continue;
                }

                if (genericArguments.Length > 0)
                {
                    methods[i] = methods[i].MakeGenericMethod(genericArguments);
                }

                if (ShouldBind(methods[i], methodArgs, visitor, out boundMethod))
                {
                    var expressions = new Expression[methodArgs.Length];
                    for (var j = 0; j < methodArgs.Length; j++)
                    {
                        expressions[j] = methodArgs[j].Expression;
                    }

                    bindingResult.Expression = Expression.Call(instance, boundMethod, expressions);
                    return bindingResult;
                }
            }

            if (!isInstance)
            {
                bindingResult.Reason = new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ErrorStrings.NoMemberArgumentMatch,
                        sourceType.FullName,
                        name));
                bindingResult.Id = nameof(ErrorStrings.NoMemberArgumentMatch);
                return bindingResult;
            }

            var extensionArgs = new MethodArgument[methodArgs.Length + 1];
            extensionArgs[0] = (MethodArgument)instance;
            for (var i = 1; i < extensionArgs.Length; i++)
            {
                extensionArgs[i] = methodArgs[i - 1];
            }

            methods = GetExtensionMethods();
            for (var i = 0; i < methods.Length; i++)
            {
                if (!methods[i].Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                if (!didFindName)
                {
                    didFindName = true;
                }

                if (genericArguments.Length > 0 &&
                    (!methods[i].IsGenericMethod ||
                    !AreGenericArgumentsValid(methods[i], genericArguments)))
                {
                        continue;
                }

                if (genericArguments.Length > 0)
                {
                    methods[i] = methods[i].MakeGenericMethod(genericArguments);
                }

                if (ShouldBind(methods[i], extensionArgs, visitor, out boundMethod))
                {
                    var expressions = new Expression[extensionArgs.Length];
                    for (var j = 0; j < extensionArgs.Length; j++)
                    {
                        expressions[j] = extensionArgs[j].Expression;
                    }

                    bindingResult.Expression = Expression.Call(boundMethod, expressions);
                    return bindingResult;
                }
            }

            if (!didFindName)
            {
                bindingResult.Reason =
                    new ArgumentException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            ErrorStrings.NoMemberNameMatch,
                            sourceType.FullName,
                            name));

                bindingResult.Id = nameof(ErrorStrings.NoMemberNameMatch);
                return bindingResult;
            }

            bindingResult.Reason = new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.NoMemberArgumentMatch,
                    sourceType.FullName,
                    name));
            bindingResult.Id = nameof(ErrorStrings.NoMemberArgumentMatch);
            return bindingResult;
        }

        private MethodInfo[] GetExtensionMethods()
        {
            if (_extensionMethods != null)
            {
                return _extensionMethods;
            }

            var extensionMethods = new List<MethodInfo>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var module in assembly.GetModules())
                {
                    foreach (var type in module.GetTypes())
                    {
                        if (string.IsNullOrEmpty(type.Namespace))
                        {
                            continue;
                        }

                        if (!_namespaces.Any(ns => ns.Equals(type.Namespace, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            continue;
                        }

                        foreach (var method in type.GetMethods(_staticFlags))
                        {
                            if (method.IsDefined(typeof(ExtensionAttribute), inherit: false))
                            {
                                extensionMethods.Add(method);
                            }
                        }
                    }
                }
            }

            _extensionMethods = extensionMethods.ToArray();
            return _extensionMethods;
        }

        private bool AreGenericArgumentsValid(MethodInfo method, Type[] genericArguments)
        {
            var genericParameters = method.GetGenericArguments();
            if (genericParameters.Length != genericArguments.Length)
            {
                return false;
            }

            for (var i = 0; i < genericParameters.Length; i++)
            {
                var constraints = genericParameters[i].GetGenericParameterConstraints();
                for (var j = 0; j < constraints.Length; j++)
                {
                    if (!constraints[j].IsAssignableFrom(genericParameters[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ShouldBind(
            MethodInfo method,
            MethodArgument[] arguments,
            CompileVisitor visitor,
            out MethodInfo resolvedMethod)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != arguments.Length)
            {
                resolvedMethod = null;
                return false;
            }

            BindingStatus status;
            status._map = new Dictionary<Type, Type>();
            status._hasGenericParams = method.IsGenericMethod;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (arguments[i].Ast != null &&
                    (arguments[i].Ast is ScriptBlockExpressionAst ||
                    arguments[i].Ast is ScriptBlockAst))
                {
                    if (!(typeof(Delegate).IsAssignableFrom(parameters[i].ParameterType) ||
                        typeof(LambdaExpression).IsAssignableFrom(parameters[i].ParameterType)))
                    {
                        resolvedMethod = null;
                        return false;
                    }

                    if (status.IsDelegateMatch(parameters[i].ParameterType, arguments[i], visitor))
                    {
                        continue;
                    }

                    resolvedMethod = null;
                    return false;
                }

                if (arguments[i].Expression == null)
                {
                    arguments[i].Expression = arguments[i].Ast.Compile(visitor);
                }

                if (!status.IsTypeMatch(parameters[i].ParameterType, arguments[i].Expression.Type))
                {
                    resolvedMethod = null;
                    return false;
                }
            }

            if (!status._hasGenericParams || !method.IsGenericMethodDefinition)
            {
                resolvedMethod = method;
                return true;
            }

            var genericParameters = method.GetGenericArguments();
            var genericArguments = new Type[genericParameters.Length];
            for (var i = 0; i < genericParameters.Length; i++)
            {
                genericArguments[i] = status._map[genericParameters[i]];
            }

            resolvedMethod = method.MakeGenericMethod(genericArguments);
            return true;
        }

        /// <summary>
        /// Represents the result of a method binding attempt.
        /// </summary>
        internal struct BindingResult
        {
            /// <summary>
            /// The generated <see cref="Expression" />. Can be <see langkeyword="null" />
            /// if the binding attempt was unsuccessful.
            /// </summary>
            internal Expression Expression;

            /// <summary>
            /// The <see cref="Exception" /> that describes the binding failure. Can be
            /// <see langkeyword="null" /> if the binding was successful.
            /// </summary>
            internal Exception Reason;

            /// <summary>
            /// The ID that should be attached to the <see cref="ParseException" />. Can
            /// be <see langkeyword="null" /> if the binding attempt was successful.
            /// </summary>
            internal string Id;
        }

        private struct BindingStatus
        {
            internal Dictionary<Type, Type> _map;

            internal bool _hasGenericParams;

            internal bool IsTypeMatch(Type parameterType, Type argumentType)
            {
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType();
                }

                if (parameterType.IsAssignableFrom(argumentType))
                {
                    return true;
                }

                if (!_hasGenericParams)
                {
                    return false;
                }

                if (argumentType.IsArray &&
                    parameterType.IsGenericType &&
                    parameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    argumentType = typeof(IEnumerable<>).MakeGenericType(
                        argumentType.GetElementType());
                }

                if (parameterType.IsPointer != argumentType.IsPointer)
                {
                    return false;
                }

                if (parameterType.HasElementType && parameterType.HasElementType)
                {
                    parameterType = parameterType.GetElementType();
                    argumentType = argumentType.GetElementType();
                }

                if (parameterType.IsGenericType && parameterType.IsGenericType)
                {
                    var parameterGenerics = parameterType.GetGenericArguments();
                    var argumentGenerics = argumentType.GetGenericArguments();

                    if (parameterGenerics.Length != argumentGenerics.Length)
                    {
                        return false;
                    }

                    for (var i = 0; i < parameterGenerics.Length; i++)
                    {
                        if (!IsTypeMatch(parameterGenerics[i], argumentGenerics[i]))
                        {
                            return false;
                        }
                    }

                    var constructedParameterType = parameterType
                        .GetGenericTypeDefinition()
                        .MakeGenericType(argumentGenerics);

                    return constructedParameterType.IsAssignableFrom(argumentType);
                }

                if (!parameterType.IsGenericParameter)
                {
                    return false;
                }

                if (_map.TryGetValue(parameterType, out Type existingResolvedType))
                {
                    return existingResolvedType.IsAssignableFrom(argumentType);
                }

                foreach (var constraint in parameterType.GetGenericParameterConstraints())
                {
                    if (!constraint.IsAssignableFrom(argumentType))
                    {
                        return false;
                    }
                }

                _map.Add(parameterType, argumentType);
                return true;
            }

            internal bool IsDelegateMatch(Type parameterType, MethodArgument argument, CompileVisitor visitor)
            {
                ScriptBlockAst sbAst;
                if (argument.Ast is ScriptBlockExpressionAst sbExpression)
                {
                    sbAst = (ScriptBlockAst)sbExpression.ScriptBlock.Visit(
                        new DelegateSyntaxVisitor(visitor.Errors));
                }
                else
                {
                    sbAst = (ScriptBlockAst)((ScriptBlockAst)argument.Ast).Visit(
                        new DelegateSyntaxVisitor(visitor.Errors));
                }

                argument.Ast = sbAst;
                var parameterMethod = parameterType.GetMethod(Strings.DelegateInvokeMethodName);
                if (parameterMethod == null && typeof(Expression).IsAssignableFrom(parameterType))
                {
                    parameterMethod = parameterType
                        .GetGenericArguments()[0]
                        .GetMethod(Strings.DelegateInvokeMethodName);
                }

                var astHasExplicitReturn = ExplicitReturnVisitor.TryFindExplicitReturn(
                    sbAst,
                    out PipelineBaseAst returnValue);

                if (astHasExplicitReturn)
                {
                    if (parameterMethod.ReturnType == typeof(void) &&
                        returnValue != null)
                    {
                        return false;
                    }
                }

                var parameterParameters = parameterMethod.GetParameters();
                ParameterAst[] sbParameters;
                if (sbAst.ParamBlock != null)
                {
                    sbParameters = new ParameterAst[sbAst.ParamBlock.Parameters.Count];
                    for (var i = 0; i < sbParameters.Length; i++)
                    {
                        sbParameters[i] = sbAst.ParamBlock.Parameters[i];
                    }
                }
                else
                {
                    sbParameters = s_emptyParameterAsts;
                }

                if (parameterParameters.Length != sbParameters.Length)
                {
                    return false;
                }

                var expectedParameterTypes = new Type[parameterParameters.Length];
                for (var i = 0; i < parameterParameters.Length; i++)
                {
                    if (parameterParameters[i].ParameterType.IsGenericParameter)
                    {
                        if (_map.TryGetValue(parameterParameters[i].ParameterType, out Type resolvedType))
                        {
                            expectedParameterTypes[i] = resolvedType;
                            continue;
                        }

                        // TODO: Check if parameter is strongly typed in the AST and use that to
                        //       resolve the targ.
                        return false;
                    }

                    expectedParameterTypes[i] = parameterParameters[i].ParameterType;
                }

                var expectedReturnType = parameterMethod.ReturnType.IsGenericParameter
                    ? null
                    : parameterMethod.ReturnType;

                if (expectedReturnType == null)
                {
                    _map.TryGetValue(parameterMethod.ReturnType, out expectedReturnType);
                }

                var oldErrorWriter = visitor.Errors;
                try
                {
                    visitor.Errors = ParseErrorWriter.CreateNull();
                    argument.Expression = visitor.CompileAstImpl(
                        sbAst,
                        s_emptyVariables,
                        expectedParameterTypes,
                        expectedReturnType,
                        null);
                }
                catch (Exception)
                {
                    // TODO: Better reporting here if all method resolution fails.
                    return false;
                }
                finally
                {
                    visitor.Errors = oldErrorWriter;
                }

                if (parameterMethod.ReturnType.IsGenericParameter &&
                    !_map.ContainsKey(parameterMethod.ReturnType))
                {
                    _map.Add(parameterMethod.ReturnType, ((LambdaExpression)argument.Expression).ReturnType);
                }

                if (parameterType.IsGenericType)
                {
                    var genericParameters = parameterType.GetGenericArguments();
                    var newGenericParameters = new Type[genericParameters.Length];
                    for (var i = 0; i < genericParameters.Length; i++)
                    {
                        if (genericParameters[i].IsGenericParameter)
                        {
                            _map.TryGetValue(genericParameters[i], out Type resolvedType);
                            newGenericParameters[i] = resolvedType;
                            continue;
                        }

                        newGenericParameters[i] = genericParameters[i];
                    }

                    parameterType = parameterType
                        .GetGenericTypeDefinition()
                        .MakeGenericType(newGenericParameters);
                }

                argument.Expression = Expression.Lambda(
                    parameterType,
                    ((LambdaExpression)argument.Expression).Body,
                    ((LambdaExpression)argument.Expression).Parameters);
                return true;
            }
        }

        private class MethodArgument
        {
            internal Ast Ast;

            internal Expression Expression;

            public static explicit operator Expression(MethodArgument argument)
            {
                return argument.Expression;
            }

            public static explicit operator MethodArgument(Ast ast)
            {
                var argument = new MethodArgument();
                argument.Ast = ast;
                return argument;
            }

            public static explicit operator MethodArgument(Expression expression)
            {
                var argument = new MethodArgument();
                argument.Expression = expression;
                return argument;
            }
        }

        private class ExplicitReturnVisitor : AstVisitor
        {
            private bool _wasFound;

            private PipelineBaseAst _returnPipeline;

            public override AstVisitAction VisitInvokeMemberExpression(InvokeMemberExpressionAst methodCallAst)
            {
                return AstVisitAction.SkipChildren;
            }

            public override AstVisitAction VisitReturnStatement(ReturnStatementAst returnStatementAst)
            {
                _wasFound = true;
                _returnPipeline = returnStatementAst.Pipeline;
                return AstVisitAction.StopVisit;
            }

            internal static bool TryFindExplicitReturn(Ast ast, out PipelineBaseAst returnValue)
            {
                var visitor = new ExplicitReturnVisitor();
                ast.Visit(visitor);
                returnValue = visitor._returnPipeline;
                return visitor._wasFound;
            }
        }
    }
}
