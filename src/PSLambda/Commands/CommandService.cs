using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Management.Automation.Language;

namespace PSLambda.Commands
{
    /// <summary>
    /// Provides management of custom command handlers.
    /// </summary>
    internal class CommandService
    {
        private readonly Dictionary<string, ICommandHandler> _commandRegistry = new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandService" /> class.
        /// </summary>
        public CommandService()
        {
            RegisterCommand(new DefaultCommand());
            RegisterCommand(new WithCommand());
            RegisterCommand(new GenericCommand());
        }

        /// <summary>
        /// Registers a command handler.
        /// </summary>
        /// <param name="handler">The handler to register</param>
        public void RegisterCommand(ICommandHandler handler)
        {
            if (_commandRegistry.ContainsKey(handler.CommandName))
            {
                return;
            }

            _commandRegistry.Add(handler.CommandName, handler);
        }

        /// <summary>
        /// Attempt to process a <see cref="CommandAst" /> as a custom command.
        /// </summary>
        /// <param name="commandAst">The <see cref="CommandAst" /> to process.</param>
        /// <param name="visitor">
        /// The <see cref="CompileVisitor" /> requesting the expression.
        /// </param>
        /// <param name="expression">
        /// The <see cref="Expression" /> result if a command handler was found.
        /// </param>
        /// <returns><c>true</c> if a commmand handler was matched, otherwise <c>false</c>.</returns>
        public bool TryProcessAst(CommandAst commandAst, CompileVisitor visitor, out Expression expression)
        {
            if (_commandRegistry.TryGetValue(commandAst.GetCommandName(), out ICommandHandler handler))
            {
                expression = handler.ProcessAst(commandAst, visitor);
                return true;
            }

            expression = null;
            return false;
        }
    }
}
