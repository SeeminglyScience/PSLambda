using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Represents an <see cref="Delegate" /> that is not yet compiled but can be
    /// converted implicitly by the PowerShell engine.
    /// </summary>
    public sealed class PSDelegate
    {
        private Delegate _defaultDelegate;

        /// <summary>
        /// Initializes a new instance of the <see cref="PSDelegate" /> class.
        /// </summary>
        /// <param name="scriptBlock">The <see cref="ScriptBlock" /> to compile.</param>
        public PSDelegate(ScriptBlock scriptBlock)
        {
            using (var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                Locals =
                    pwsh.AddCommand("Microsoft.PowerShell.Utility\\Get-Variable")
                        .AddParameter("Scope", 0)
                        .Invoke<PSVariable>()
                        .Where(v => !SpecialVariables.IgnoreLocal.Contains(v.Name))
                        .ToDictionary(v => v.Name);

                pwsh.Commands.Clear();

                EngineIntrinsics =
                    pwsh.AddCommand("Microsoft.PowerShell.Utility\\Get-Variable")
                        .AddParameter("Name", "ExecutionContext")
                        .AddParameter("ValueOnly", true)
                        .Invoke<EngineIntrinsics>()
                        .First();
            }

            ScriptBlock = scriptBlock;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PSDelegate" /> class.
        /// </summary>
        /// <param name="scriptBlock">The <see cref="ScriptBlock" /> to compile.</param>
        /// <param name="engine">
        /// The <see cref="EngineIntrinsics" /> instance for the current runspace.
        /// </param>
        /// <param name="locals">
        /// Any <see cref="PSVariable" /> objects that are local to the
        /// current scope with the exception of AllScope variables.
        /// </param>
        internal PSDelegate(ScriptBlock scriptBlock, EngineIntrinsics engine, Dictionary<string, PSVariable> locals)
        {
            Locals = locals;
            EngineIntrinsics = engine;
            ScriptBlock = scriptBlock;
        }

        /// <summary>
        /// Gets the ScriptBlock represented by the delegate.
        /// </summary>
        public ScriptBlock ScriptBlock { get; }

        /// <summary>
        /// Gets the <see cref="EngineIntrinsics" /> from the origin
        /// <see cref="System.Management.Automation.Runspaces.Runspace" />.
        /// </summary>
        internal EngineIntrinsics EngineIntrinsics { get; }

        /// <summary>
        /// Gets the <see cref="PSVariable" /> objects from origin SessionStateScope.
        /// </summary>
        internal Dictionary<string, PSVariable> Locals { get; }

        /// <summary>
        /// Gets the default <see cref="Delegate" /> that is used for the
        /// <see cref="PSDelegate.Invoke(object[])" /> method.
        /// </summary>
        internal Delegate DefaultDelegate
        {
            get
            {
                if (_defaultDelegate != null)
                {
                    return _defaultDelegate;
                }

                return _defaultDelegate = CreateDefaultDelegate();
            }
        }

        /// <summary>
        /// Invokes the compiled <see cref="Delegate" /> represented by this object. If the
        /// <see cref="Delegate" /> has not yet been compiled, it will be compiled prior to
        /// invocation.
        /// </summary>
        /// <param name="arguments">Arguments to pass to the <see cref="Delegate" />.</param>
        /// <returns>The result returned by the <see cref="Delegate" />.</returns>
        public object Invoke(params object[] arguments)
        {
            return DefaultDelegate.DynamicInvoke(arguments);
        }

        private Delegate CreateDefaultDelegate()
        {
            return CompileVisitor.CompileAst(
                EngineIntrinsics,
                (ScriptBlockAst)ScriptBlock.Ast,
                Locals.Values.ToArray());
        }
    }
}
