$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'basic loop functionality' {
    It 'for statement' {
        $delegate = New-PSDelegate {
            [int] $total = 0
            for ([int] $i = 0; $i -lt 10; $i++) {
                $total = $i + $total
            }

            return $total
        }

        $delegate.Invoke() | Should -Be 45
    }

    It 'foreach statement' {
        $delegate = New-PSDelegate {
            [int[]] $numbers = 1, 2, 3, 4
            [int] $total = 0

            foreach($item in $numbers) {
                $total = [int]$item + [int]$total
            }

            return $total
        }

        $delegate.Invoke() | Should -Be 10
    }

    It 'while statement' {
        $delegate = New-PSDelegate {
            [int] $i = 0
            while ($i -lt 10) {
                $i++
            }

            return $i
        }

        $delegate.Invoke() | Should -Be 10
    }

    It 'do while statement' {
        $delegate = New-PSDelegate {
            [int] $i = 0
            do {
                $i++
            } while ($i -lt 10)

            return $i
        }

        $delegate.Invoke() | Should -Be 10
    }

    It 'do until statement' {
        $delegate = New-PSDelegate {
            [int] $i = 0
            do {
                $i++
            } until ($i -gt 10)

            return $i
        }

        $delegate.Invoke() | Should -Be 11
    }
}
