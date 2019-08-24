using System;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Represents the current set of variable scopes for a block <see cref="Expression" /> objects.
    /// </summary>
    internal class VariableScopeStack
    {
        private static readonly ParameterExpression[] s_emptyParameters = new ParameterExpression[0];

        private VariableScope _current;

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableScopeStack" /> class.
        /// </summary>
        internal VariableScopeStack()
        {
            _current = new VariableScope(s_emptyParameters);
        }

        /// <summary>
        /// Creates a new variable scope.
        /// </summary>
        /// <returns>
        /// A <see cref="IDisposable" /> handle that will return to the previous scope when disposed.
        /// </returns>
        internal IDisposable NewScope()
        {
            _current = new VariableScope(_current);
            return new ScopeHandle(() => _current = _current?.Parent);
        }

        /// <summary>
        /// Creates a new variable scope.
        /// </summary>
        /// <param name="parameters">Parameters that should be available from the new scope.</param>
        /// <returns>
        /// A <see cref="IDisposable" /> handle that will return to the previous scope when disposed.
        /// </returns>
        internal IDisposable NewScope(ParameterExpression[] parameters)
        {
            var handle = NewScope();
            var currentScope = _current;
            foreach (var parameter in parameters)
            {
                currentScope.Parameters.Add(parameter.Name, parameter);
            }

            return handle;
        }

        /// <summary>
        /// Creates a new variable expression without looking at previous scopes.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="type">The type of the variable.</param>
        /// <returns>The variable <see cref="Expression" />.</returns>
        internal ParameterExpression NewVariable(string name, Type type)
        {
            return _current.NewVariable(name, type);
        }

        /// <summary>
        /// Gets a variable <see cref="Expression" /> from the current or a parent scope.
        /// </summary>
        /// <param name="variableExpressionAst">
        /// The <see cref="VariableExpressionAst" /> to obtain a variable <see cref="Expression" /> for.
        /// </param>
        /// <param name="alreadyDefined">
        /// A value indicating whether the variable has already been defined.
        /// </param>
        /// <returns>The resolved variable <see cref="Expression" />.</returns>
        internal ParameterExpression GetVariable(
            VariableExpressionAst variableExpressionAst,
            out bool alreadyDefined)
        {
            return _current.GetOrCreateVariable(variableExpressionAst.VariablePath.UserPath, variableExpressionAst.StaticType, out alreadyDefined);
        }

        /// <summary>
        /// Gets a variable <see cref="Expression" /> from the current or a parent scope.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="type">The type of the variable.</param>
        /// <returns>The resolved variable <see cref="Expression" />.</returns>
        internal ParameterExpression GetVariable(string name, Type type)
        {
            return _current.GetOrCreateVariable(name, type);
        }

        /// <summary>
        /// Gets all variables declared in the current scope.
        /// </summary>
        /// <returns>
        /// The variable <see cref="Expression" /> objects declared in the current scope.
        /// </returns>
        internal ParameterExpression[] GetVariables()
        {
            return _current.Variables.Values.ToArray();
        }

        /// <summary>
        /// Gets all parameters declared in the current scope.
        /// </summary>
        /// <returns>
        /// The parameter <see cref="Expression" /> objects declared in the current scope.
        /// </returns>
        internal ParameterExpression[] GetParameters()
        {
            return _current.Parameters.Values.ToArray();
        }

        /// <summary>
        /// Gets the automatic variable <c>$_</c> or <c>$PSItem</c> from the
        /// current scope, or closest parent scope where it is defined.
        /// </summary>
        /// <param name="dollarUnder">
        /// The parameter expression referencing dollar under if defined;
        /// otherwise <see langword="null" />.
        /// </param>
        /// <returns>
        /// <see langword="true" /> if a defined dollar under variable was
        /// found; otherwise <see langword="false" />.
        /// </returns>
        internal bool TryGetDollarUnder(out ParameterExpression dollarUnder)
        {
            dollarUnder = GetDollarUnder();
            return dollarUnder != null;
        }

        /// <summary>
        /// Gets the automatic variable <c>$_</c> or <c>$PSItem</c> from the
        /// current scope, or closest parent scope where it is defined.
        /// </summary>
        /// <returns>
        /// The parameter expression referencing dollar under if defined;
        /// otherwise <see langword="null" />.
        /// </returns>
        internal ParameterExpression GetDollarUnder() => _current.GetDollarUnder();

        /// <summary>
        /// Sets the automatic variable <c>$_</c> or <c>$PSItem</c> in the current
        /// scope. If the variable exists in a parent scope and the type is the same,
        /// then the parent variable will be used. If the variable exists in a parent
        /// scope but the type is not the same, the variable will be renamed behind
        /// the scenes.
        /// </summary>
        /// <param name="type">
        /// The static type that dollar under should contain.
        /// </param>
        /// <returns>
        /// The parameter expression referencing dollar under.
        /// </returns>
        internal ParameterExpression SetDollarUnder(Type type) => _current.SetDollarUnder(type);
    }
}
