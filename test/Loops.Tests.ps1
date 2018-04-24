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

    It 'break continues after loop' {
        $delegate = New-PSDelegate {
            $i = 0
            $continuedAfterBreak = $false
            while ($i -lt 10) {
                $i++
                if ($i -eq 5) {
                    break
                    $continuedAfterBreak = $true
                }
            }

            if ($continuedAfterBreak) {
                throw 'code after "break" was executed'
            }

            return $i
        }

        $delegate.Invoke() | Should -Be 5
    }

    It 'continue steps to next interation' {
        $delegate = New-PSDelegate {
            $i = 0
            $continuedAfterContinue = $false
            while ($i -lt 10) {
                $i++
                continue
                $continuedAfterContinue = $true
            }

            if ($continuedAfterContinue) {
                throw 'code after "continue" was executed'
            }

            return $i
        }

        $delegate.Invoke() | Should -Be 10
    }

    Context 'switch statement' {
        It 'chooses correct value' {
            $hitValues = [System.Collections.Generic.List[string]]::new()
            $delegate = New-PSDelegate {
                foreach ($value in 'value1', 'value2', 'value3', 'invalid') {
                    switch ($value) {
                        value1 { $hitValues.Add('option1') }
                        value2 { $hitValues.Add('option2') }
                        value3 { $hitValues.Add('option3') }
                        default { throw }
                    }
                }
            }

            { $delegate.Invoke() } | Should -Throw
            $hitValues | Should -Be 'option1', 'option2', 'option3'
        }

        It 'can have a single value' {
            $delegate = New-PSDelegate {
                switch ('value') {
                    value { return 'value' }
                }
            }

            $delegate.Invoke() | Should -Be 'value'
        }

        It 'can have a default without cases' {
            $delegate = New-PSDelegate {
                switch ('value') {
                    default { return 'value' }
                }
            }

            $delegate.Invoke() | Should -Be 'value'
        }
    }
}
