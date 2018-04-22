$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'If statements' {
    It 'if statement only' {
        $delegate = New-PSDelegate {
            ([int] $a) => {
                if ($a -eq 1) {
                    return $true
                }

                return $false
            }
        }

        $delegate.Invoke(1) | Should -Be $true
        $delegate.Invoke(0) | Should -Be $false
    }

    It 'if else' {
        $delegate = New-PSDelegate {
            ([int] $a) => {
                if ($a -eq 1) {
                    return 'if'
                } else {
                    return 'else'
                }
            }
        }

        $delegate.Invoke(1) | Should -Be 'if'
        $delegate.Invoke(0) | Should -Be 'else'
    }

    It 'if else if' {
        $delegate = New-PSDelegate {
            ([int] $a) => {
                if ($a -eq 1) {
                    return 'if'
                } elseif ($a -eq 2) {
                    return 'elseif'
                }

                return 'none'
            }
        }

        $delegate.Invoke(2) | Should -Be 'elseif'
        $delegate.Invoke(1) | Should -Be 'if'
        $delegate.Invoke(0) | Should -Be 'none'
    }

    It 'if else-if else' {
        $delegate = New-PSDelegate {
            ([int] $a) => {
                if ($a -eq 1) {
                    return 'if'
                } elseif ($a -eq 2) {
                    return 'elseif'
                } else {
                    return 'else'
                }
            }
        }

        $delegate.Invoke(2) | Should -Be 'elseif'
        $delegate.Invoke(1) | Should -Be 'if'
        $delegate.Invoke(0) | Should -Be 'else'
    }

    It 'if with multiple else-ifs' {
        $delegate = New-PSDelegate {
            ([int] $a) => {
                if ($a -eq 1) {
                    return 'if'
                } elseif ($a -eq 2) {
                    return 'elseif'
                } elseif ($a -eq 3) {
                    return 'elseif1'
                } elseif ($a -eq 4) {
                    return 'elseif2'
                } else {
                    return 'else'
                }
            }
        }

        $delegate.Invoke(4) | Should -Be 'elseif2'
        $delegate.Invoke(3) | Should -Be 'elseif1'
        $delegate.Invoke(2) | Should -Be 'elseif'
        $delegate.Invoke(1) | Should -Be 'if'
        $delegate.Invoke(0) | Should -Be 'else'
    }
}
