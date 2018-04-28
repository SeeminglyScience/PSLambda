using namespace System
using namespace System.Management.Automation
using namespace System.Management.Automation.Language

$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'Method resolution tests' {
    It 'can resolve generic parameters from usage' {
        $delegate = New-PSDelegate {
            $ExecutionContext.InvokeProvider.ChildItem.Get('function:', $true).
                Select{ $pso => $pso.BaseObject }.
                OfType([g[System.Management.Automation.FunctionInfo]]).
                FirstOrDefault{ $f => $f.Name.Equals('TabExpansion2') }
        }

        $delegate.Invoke().Name | Should -Be TabExpansion2
    }

    It 'can resolve extension methods other than Linq' {
        $sb = [scriptblock]::Create('
            using namespace System.Threading.Tasks

            New-PSDelegate {
                $task = [Task]::FromResult([Task]::Run{ return "testing" })
                return $task.Unwrap()
            }')

        $delegate = $sb.Invoke()
        $delegate.Invoke().GetAwaiter().GetResult() | Should -Be testing
    }

    It 'can pass variables by ref' {
        $delegate = New-PSDelegate {
            [Token[]] $tokens = $null
            [ParseError[]] $errors = $null
            $ast = [Parser]::ParseInput('{', $tokens, $errors)
            return [Tuple]::Create($ast, $tokens, $errors)
        }

        $result = $delegate.Invoke()
        $result.Item1 | Should -BeOfType System.Management.Automation.Language.Ast
        $result.Item2.GetType() | Should -Be ([Token[]])
        $result.Item2.Length | Should -Be 2
        $result.Item3.GetType() | Should -Be ([ParseError[]])
        $result.Item3.Length | Should -Be 1
    }

    It 'can convert delegates to other than Func/Action ' {
        $delegate = New-PSDelegate {
            $list = [System.Collections.Generic.List[int]]::new(0..10)
            return $list.ConvertAll{ $i => $i.ToString() }
        }

        $delegate.Invoke().GetType() | Should -Be ([System.Collections.Generic.List[string]])
    }

    It 'can resolve many generic parameters' {
        $delegate = New-PSDelegate {
            [Tuple]::Create(
                10,
                [Exception]::new(),
                'string',
                $Host,
                [runspace]::DefaultRunspace,
                [ConsoleColor]::Black,
                @{}).
                ToValueTuple()
        }

        $delegate.Invoke().GetType() |
            Should -Be (
                [ValueTuple[int, exception, string, Host.PSHost, runspace, ConsoleColor, hashtable]])
    }

    It 'throws the correct message when a member does not exist' {
        $expectedMsg =
            "'System.String' does not contain a definition for 'RandomName' " +
            "and no extension method 'RandomName' accepting a first argument " +
            "of type 'System.String' could be found."

        { New-PSDelegate { ''.RandomName() }} | Should -Throw $expectedMsg
    }

    It 'throws the correct message when method arguments do not match' {
        $expectedMsg =
            "'System.String' does not contain a definition for a method " +
            "named 'Compare' that takes the specified arguments."

        { New-PSDelegate { [string]::Compare() }} | Should -Throw $expectedMsg

        $expectedMsg =
            "'System.Management.Automation.Host.PSHostUserInterface' does not " +
            "contain a definition for a method named 'WriteLine' that takes the " +
            "specified arguments."

        { New-PSDelegate { [Collections.Generic.List[int]]::new(0..10).ConvertAll('invalid') } } |
            Should -Throw $expectedMsg
    }
}
