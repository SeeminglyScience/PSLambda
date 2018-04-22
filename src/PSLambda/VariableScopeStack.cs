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
    }
}
