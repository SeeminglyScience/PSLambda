using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;

namespace PSLambda
{
    /// <summary>
    /// Represents the New-PSDelegate command.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "PSDelegate")]
    [OutputType(typeof(Delegate))]
    public class NewPSDelegateCommand : PSCmdlet
    {
        /// <summary>
        /// Gets or sets the value for the parameter "DelegateType".
        /// </summary>
        [Parameter]
        [ValidateNotNull]
        public Type DelegateType { get; set; }

        /// <summary>
        /// Gets or sets the value for the parameter "Expression".
        /// </summary>
        [Parameter(Position = 0, Mandatory = true)]
        [ValidateNotNull]
        public ScriptBlock Expression { get; set; }

        /// <summary>
        /// The EndProcessing method.
        /// </summary>
        protected override void EndProcessing()
        {
            var variables = SessionState.InvokeCommand.InvokeScript(
                "Get-Variable -Scope 0",
                false,
                PipelineResultTypes.Output,
                null,
                null)
                .Select(pso => pso.BaseObject)
                .Cast<PSVariable>()
                .Where(v => !SpecialVariables.IgnoreLocal.Contains(v.Name));

            try
            {
                if (DelegateType == null)
                {
                    WriteObject(
                        CompileVisitor.CompileAst(
                            (EngineIntrinsics)SessionState.PSVariable.GetValue(Strings.ExecutionContextVariableName),
                            (ScriptBlockAst)Expression.Ast,
                            variables.ToArray()),
                        enumerateCollection: false);
                    return;
                }

                WriteObject(
                    CompileVisitor.CompileAst(
                        (EngineIntrinsics)SessionState.PSVariable.GetValue(Strings.ExecutionContextVariableName),
                        (ScriptBlockAst)Expression.Ast,
                        variables.ToArray(),
                        DelegateType),
                    enumerateCollection: false);
            }
            catch (ParseException e)
            {
                ThrowTerminatingError(new ErrorRecord(e.ErrorRecord, e));
            }
        }
    }
}
