using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace PSLambda
{
    /// <summary>
    /// Represents the variable scope for a single block <see cref="Expression" />.
    /// </summary>
    internal class VariableScope
    {
        private static readonly string[] s_dollarUnderNameCache =
        {
            "PSItem", "PSItem2", "PSItem3", "PSItem4", "PSItem5",
            "PSItem6", "PSItem7", "PSItem8", "PSItem9", "PSItem10",
        };

        private (ParameterExpression Parameter, int Version) _dollarUnder;

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableScope" /> class.
        /// </summary>
        /// <param name="parameters">Parameters to include in the scope.</param>
        public VariableScope(ParameterExpression[] parameters)
        {
            Parameters = parameters.ToDictionary(p => p.Name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableScope" /> class.
        /// </summary>
        /// <param name="parent">The parent <see cref="VariableScope" /> object.</param>
        public VariableScope(VariableScope parent)
        {
            Parent = parent;
        }

        /// <summary>
        /// Gets the parent <see cref="VariableScope" />.
        /// </summary>
        internal VariableScope Parent { get; }

        /// <summary>
        /// Gets the parameter <see cref="Expression" /> objects for the current scope.
        /// </summary>
        internal Dictionary<string, ParameterExpression> Parameters { get; } = new Dictionary<string, ParameterExpression>();

        /// <summary>
        /// Gets the variable <see cref="Expression" /> objects for the current scope.
        /// </summary>
        internal Dictionary<string, ParameterExpression> Variables { get; } = new Dictionary<string, ParameterExpression>();

        /// <summary>
        /// Creates a new variable expression without looking at previous scopes.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="type">The type of the variable.</param>
        /// <returns>The variable <see cref="Expression" />.</returns>
        internal ParameterExpression NewVariable(string name, Type type)
        {
            ParameterExpression variable = Expression.Variable(type, name);
            Variables.Add(name, variable);
            return variable;
        }

        /// <summary>
        /// Gets an already defined variable <see cref="Expression" /> if it already exists,
        /// otherwise one is created.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="type">The type of the variable.</param>
        /// <returns>The variable <see cref="Expression" />.</returns>
        internal ParameterExpression GetOrCreateVariable(string name, Type type)
        {
            return GetOrCreateVariable(name, type, out _);
        }

        /// <summary>
        /// Gets an already defined variable <see cref="Expression" /> if it already exists,
        /// otherwise one is created.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="type">The type of the variable.</param>
        /// <param name="alreadyDefined">
        /// A value indicating whether the variable has already been defined.
        /// </param>
        /// <returns>The variable <see cref="Expression" />.</returns>
        internal ParameterExpression GetOrCreateVariable(string name, Type type, out bool alreadyDefined)
        {
            if (TryGetVariable(name, out ParameterExpression existingVar))
            {
                alreadyDefined = true;
                return existingVar;
            }

            alreadyDefined = false;
            var newVariable = Expression.Parameter(type ?? typeof(object), name);
            Variables.Add(name, newVariable);
            return newVariable;
        }

        /// <summary>
        /// Gets an already defined variable <see cref="Expression" /> if it already exists,
        /// otherwise one is created.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="typeGetter">
        /// A function that retrieves the variable's type if it has not already been defined.
        /// </param>
        /// <returns>The variable <see cref="Expression" />.</returns>
        internal ParameterExpression GetOrCreateVariable(string name, Func<Type> typeGetter)
        {
            if (TryGetVariable(name, out ParameterExpression existingVar))
            {
                return existingVar;
            }

            var newVariable = Expression.Parameter(typeGetter() ?? typeof(object), name);
            Variables.Add(name, newVariable);
            return newVariable;
        }

        /// <summary>
        /// Attempts to get a variable that has already been defined.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="existingVariable">The already defined variable <see cref="Expression" />.</param>
        /// <returns>
        /// A value indicating whether an existing variable <see cref="Expression" /> was found.
        /// </returns>
        internal bool TryGetVariable(string name, out ParameterExpression existingVariable)
        {
            if (Variables.TryGetValue(name, out existingVariable))
            {
                return true;
            }

            if (Parameters.TryGetValue(name, out existingVariable))
            {
                return true;
            }

            if (Parent != null && Parent.TryGetVariable(name, out existingVariable))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the automatic variable <c>$_</c> or <c>$PSItem</c> from the
        /// current scope, or closest parent scope where it is defined.
        /// </summary>
        /// <returns>
        /// The parameter expression referencing dollar under if defined;
        /// otherwise <see langword="null" />.
        /// </returns>
        internal ParameterExpression GetDollarUnder()
        {
            for (VariableScope current = this; current != null; current = current.Parent)
            {
                if (current._dollarUnder.Parameter != null)
                {
                    return current._dollarUnder.Parameter;
                }
            }

            return null;
        }

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
        internal ParameterExpression SetDollarUnder(Type type)
        {
            static string GetDollarUnderName(int version)
            {
                if (version < s_dollarUnderNameCache.Length)
                {
                    return s_dollarUnderNameCache[version];
                }

                return string.Concat("PSItem", version);
            }

            for (VariableScope scope = Parent; scope != null; scope = scope.Parent)
            {
                var dollarUnder = scope._dollarUnder;
                if (dollarUnder.Parameter == null)
                {
                    continue;
                }

                if (_dollarUnder.Parameter.Type == type)
                {
                    _dollarUnder = dollarUnder;
                    return _dollarUnder.Parameter;
                }

                var version = dollarUnder.Version + 1;
                _dollarUnder = (Expression.Parameter(type, GetDollarUnderName(version)), version);
                return _dollarUnder.Parameter;
            }

            _dollarUnder = (Expression.Parameter(type, "PSItem"), 0);
            return _dollarUnder.Parameter;
        }
    }
}
