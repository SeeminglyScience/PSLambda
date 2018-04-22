using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Host;

namespace PSLambda
{
    /// <summary>
    /// Provides information about special variables created by the PowerShell engine.
    /// </summary>
    internal static class SpecialVariables
    {
        /// <summary>
        /// Contains the names and types of variables that should be available from any scope.
        /// </summary>
        internal static readonly Dictionary<string, Type> AllScope = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "?", typeof(bool) },
            { "ExecutionContext", typeof(EngineIntrinsics) },
            { "Home", typeof(string) },
            { "Host", typeof(PSHost) },
            { "PID", typeof(int) },
            { "PSCulture", typeof(string) },
            { "PSHome", typeof(string) },
            { "PSUICulture", typeof(string) },
            { "PSVersionTable", typeof(System.Collections.Hashtable) },
            { "PSEdition", typeof(string) },
            { "ShellId", typeof(string) },
            { "MaximumHistoryCount", typeof(int) }
        };

        /// <summary>
        /// Provides the names and constant <see cref="Expression" /> objects for variables that
        /// should are language features like <c>true</c>.
        /// </summary>
        internal static readonly Dictionary<string, Expression> Constants = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase)
        {
            { Strings.TrueVariableName, Expression.Constant(true, typeof(bool)) },
            { Strings.FalseVariableName, Expression.Constant(false, typeof(bool)) },
            { Strings.NullVariableName, Expression.Constant(null) }
        };

        /// <summary>
        /// Provides the names of variables that should be ignored when aquiring local
        /// scope variables.
        /// </summary>
        internal static readonly HashSet<string> IgnoreLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "?",
            "ExecutionContext",
            "Home",
            "Host",
            "PID",
            "PSCulture",
            "PSHome",
            "PSUICulture",
            "PSVersionTable",
            "PSEdition",
            "ShellId",
            "MaximumHistoryCount",
            "MyInvocation",
            "OFS",
            "OutputEncoding",
            "VerboseHelpErrors",
            "LogEngineHealthEvent",
            "LogEngineLifecycleEvent",
            "LogCommandHealthEvent",
            "LogCommandLifecycleEvent",
            "LogProviderHealthEvent",
            "LogProviderLifecycleEvent",
            "LogSettingsEvent",
            "PSLogUserData",
            "NestedPromptLevel",
            "CurrentlyExecutingCommand",
            "PSBoundParameters",
            "Matches",
            "LASTEXITCODE",
            "PSDebugContext",
            "StackTrace",
            "^",
            "$",
            "?",
            "args",
            "input",
            "error",
            "PSEmailServer",
            "PSDefaultParameterValues",
            "PSScriptRoot",
            "PSCommandPath",
            "PSSenderInfo",
            "foreach",
            "switch",
            "PWD",
            "null",
            "true",
            "false",
            "PSModuleAutoLoadingPreference",
            "IsLinux",
            "IsMacOS",
            "IsWindows",
            "IsCoreCLR",
            "DebugPreference",
            "WarningPreference",
            "ErrorActionPreference",
            "InformationPreference",
            "ProgressPreference",
            "VerbosePreference",
            "WhatIfPreference",
            "ConfirmPreference",
            "ErrorView",
            "PSSessionConfigurationName",
            "PSSessionApplicationName",
            "ExecutionContext",
            "Host",
            "PID",
            "PSCulture",
            "PSHOME",
            "PSUICulture",
            "PSVersionTable",
            "PSEdition",
            "ShellId"
        };
    }
}
