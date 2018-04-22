$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'variable scoping tests' {
    It 'can access current scope variables' {
        & {
            $v = 'test'
            $delegate = New-PSDelegate { $v }
            $delegate.Invoke() | Should -Be test
        }
    }

    It 'can change current scope variables' {
        & {
            $v = 'test'
            $delegate = New-PSDelegate { $v = 'newvalue' }
            $delegate.Invoke()
            $v | Should -Be newvalue
        }
    }

    It 'can not access variables outside of the current scope' {
        $v = 'test'
        & {
            { (New-PSDelegate { $v }).Invoke()} | Should -Throw 'The variable "v" was referenced before'
        }
    }

    It 'can access all scope variables' {
        $delegate = New-PSDelegate { $ExecutionContext }
        $delegate.Invoke() | Should -BeOfType System.Management.Automation.EngineIntrinsics
    }

    It 'property types "strongly typed" PowerShell variables' {
        [int] $definitelyNewVar = 0
        $delegate = New-PSDelegate { $definitelyNewVar + 1 }
        $delegate.Invoke() | Should -Be 1
    }
}
