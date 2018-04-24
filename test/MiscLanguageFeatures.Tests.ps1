$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'Misc Language Features' {
    It 'Hashtable expression' {
        $delegate = New-PSDelegate {
    Context 'hashtable tests' {
        It 'handles varied types of values' {
            $delegate = New-PSDelegate {
            return @{
                'string' = 'value'
                string2 = 10
                Object = [object]::new()
            }
        }

        $hashtable = $delegate.Invoke()
        $hashtable['string'] | Should -Be 'value'
        $hashtable['string2'] | Should -Be 10
        $hashtable['Object'] | Should -BeOfType object
        $hashtable['object'] | Should -BeOfType object
    }

        It 'can initialize an empty hashtable' {
            (New-PSDelegate { @{} }).Invoke().GetType() | Should -Be ([hashtable])
        }
    }

    Context 'array literal' {
        It 'int' {
            $result = (New-PSDelegate { 1, 2, 3 }).Invoke()
            $result.GetType() | Should -Be ([int[]])
            $result | Should -Be 1, 2, 3
        }

        It 'string' {
            $result = (New-PSDelegate { 'one', 'two', 'three' }).Invoke()
            $result.GetType() | Should -Be ([string[]])
            $result | Should -Be one, two, three
        }

        It 'bool' {
            $result = (New-PSDelegate { $true, $false, $true }).Invoke()
            $result.GetType() | Should -Be ([bool[]])
            $result | Should -Be $true, $false, $true
        }

        It 'type' {
            $result = (New-PSDelegate { [type], [string], [int] }).Invoke()
            $result.GetType() | Should -Be ([type[]])
            $result | Should -Be ([type], [string], [int])
        }
    }

    Context 'array initializer' {
        It 'does not double arrays' {
            $result = (New-PSDelegate { @(0, 1) }).Invoke()
            $result.GetType() | Should -Be ([int[]])
            $result[0].GetType() | Should -Be ([int])
            $result | Should -Be 0, 1
        }

        It 'does create an array for single items' {
            $result = (New-PSDelegate { @(1) }).Invoke()
            $result.GetType() | Should -Be ([int[]])
            $result[0].GetType() | Should -Be ([int])
            $result | Should -Be 1
        }
    }

    Context 'assignments' {
        It 'can infer type of rhs' {
            $result = (New-PSDelegate { $a = 10; return $a++ }).Invoke()
            $result | Should -BeOfType int
            $result | Should -Be 11
        }

        It 'can access variables assigned from parent scopes' {
            $result = (New-PSDelegate {
                $a = 10
                if ($true) {
                    $a += 1
                }

                return $a
            }).Invoke()

            $result | Should -Be 11
        }

        It 'can not access a variable declared in a child scope' {
            { (New-PSDelegate { if ($true) { $a = 10 }; return $a }).Invoke() } |
                Should -Throw 'The variable "a" was referenced before it was defined or was defined in a sibling scope'
        }
    }
}
