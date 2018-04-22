$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'try/catch/finally tests' {
    It 'catch all' {
        $delegate = New-PSDelegate {
            $didFail = $false
            try {
                throw
            } catch {
                $didFail = $true
            }

            return $didFail
        }

        $delegate.Invoke() | Should -Be $true
    }

    It 'catch without matching type throws' {
        $delegate = New-PSDelegate {
            $didFail = $false
            try {
                throw
            } catch [InvalidOperationException] {
                $didFail = $true
            }

            return $didFail
        }

        { $delegate.Invoke() } | Should -Throw 'ScriptHalted'
    }

    It 'catch with filter can does not throw' {
        $delegate = New-PSDelegate {
            $didFail = $false
            try {
                throw [InvalidOperationException]::new()
            } catch [InvalidOperationException] {
                $didFail = $true
            }

            return $didFail
        }

        $delegate.Invoke() | Should -Be $true
    }

    It 'catch with multiple filters' {
        $delegate = New-PSDelegate {
            $catch = 'none'
            try {
                throw [InvalidOperationException]::new()
            } catch [ArgumentException] {
                $catch = 'argument'
            } catch [InvalidOperationException] {
                $catch = 'invalid operation'
            }
            catch {
                $catch = 'catch all'
            }

            return $catch
        }

        $delegate.Invoke() | Should -Be 'invalid operation'
    }

    It 'finally fires when thrown with no catch' {
        $didFail = $false
        $delegate = New-PSDelegate {
            try {
                throw
            } finally {
                $didFail = $true
            }
        }

        { $delegate.Invoke() } | Should -Throw ScriptHalted
        $didFail | Should -Be $true
    }

    It 'finally fires when caught' {
        $didFire = $false
        $delegate = New-PSDelegate {
            try {
                throw
            } catch {
                return
            } finally {
                $didFire = $true
            }
        }

        $delegate.Invoke()
        $didFire | Should -Be $true
    }

    It 'finally fires with no catch block' {
        $didFire = $false
        $delegate = New-PSDelegate {
            try {
                return
            } finally {
                $didFire = $true
            }
        }

        $delegate.Invoke()
        $didFire | Should -Be $true
    }
}
