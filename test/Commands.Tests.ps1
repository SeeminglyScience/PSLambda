using namespace System.Management.Automation
using namespace System.Collections.ObjectModel

$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'Custom command handlers' {
    It 'with' {
        $delegate = [psdelegate]{
            $ps = [powershell]::Create([RunspaceMode]::NewRunspace)
            with ($ps) {
                $ps.AddScript('$ExecutionContext').Invoke()
            }

            if ($ps.Runspace.RunspaceStateInfo.State -ne [Runspaces.RunspaceState]::Closed) {
                throw [RuntimeException]::new('Runspace was not closed after the with statement ended.')
            }
        }

        $delegate.Invoke()
    }

    It 'generic' {
        ([psdelegate]{ generic([array]::Empty(), [int]) }).Invoke().GetType() | Should -Be ([int[]])
    }

    It 'default' {
        ([psdelegate]{ default([ConsoleColor]) }).Invoke() | Should -Be ([System.ConsoleColor]::Black)
    }
}
