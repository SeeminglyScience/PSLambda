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
    }
}
