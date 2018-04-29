$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'Misc Language Features' {
    It 'expandable string expression' {
        $delegate = New-PSDelegate {
            $one = "1"
            $two = 2
            $three = 'something'
            return "This is a string with $one random $two numbers and $three"
        }

        $delegate.Invoke() | Should -Be 'This is a string with 1 random 2 numbers and something'
    }

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

        It 'creates an empty array' {
            $result = (New-PSDelegate { @() }).Invoke()
            $result.GetType() | Should -Be ([object[]])
            $result.Length | Should -Be 0
        }

        It 'can take multiple statements in an array' {
            $delegate = New-PSDelegate {
                return @(
                    0..10
                    10..20)
            }

            $result = $delegate.Invoke()
            $result.GetType() | Should -Be ([int[][]])
            $result.Count | Should -Be 2
            $result[0] | Should -Be (0..10)
            $result[1] | Should -Be (10..20)
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

        It 'can assign to an index operation' {
            $delegate = New-PSDelegate {
                $hash = @{ Key = 'Value' }
                $hash['Key'] = 'NewValue'
                return $hash
            }

            $delegate.Invoke().Key | Should -Be 'NewValue'
        }

        It 'can assign to a property' {
            $delegate = New-PSDelegate {
                $verboseRecord = [System.Management.Automation.VerboseRecord]::new('original message')
                $verboseRecord.Message = 'new message'
                return $verboseRecord
            }

            $delegate.Invoke().Message | Should -Be 'new message'
        }

        It 'minus equals' {
            $delegate = New-PSDelegate {
                $a = 10
                $a -= 5
                return $a
            }

            $delegate.Invoke() | Should -Be 5
        }

        It 'multiply equals' {
            $delegate = New-PSDelegate {
                $a = 10
                $a *= 5
                return $a
            }

            $delegate.Invoke() | Should -Be 50
        }

        It 'divide equals' {
            $delegate = New-PSDelegate {
                $a = 10
                $a /= 5
                return $a
            }

            $delegate.Invoke() | Should -Be 2
        }

        It 'remainder equals' {
            $delegate = New-PSDelegate {
                $a = 10
                $a %= 6
                return $a
            }

            $delegate.Invoke() | Should -Be 4
        }
    }

    Context 'indexer inference' {
        It 'indexed IList<> are typed property' {
            $delegate = New-PSDelegate {
                $list = [System.Collections.Generic.List[string]]::new()
                $list.Add('test')
                return $list[0].EndsWith('t')
            }

            $delegate.Invoke() | Should -Be $true
        }

        It 'indexed IDictionary<,> are typed property' {
            $delegate = New-PSDelegate {
                $list = [System.Collections.Generic.Dictionary[string, type]]::new()
                $list.Add('test', [type])
                return $list['test'].Namespace
            }

            $delegate.Invoke() | Should -Be 'System'
        }

        It 'can index IList' {
            $delegate = New-PSDelegate {
                $list = [System.Collections.ArrayList]::new()
                $list.AddRange([object[]]('one', 'two', 'three'))
                return [string]$list[1] -eq 'two'
            }

            $delegate.Invoke() | Should -Be $true
        }

        It 'can index custom indexers' {
            $delegate = New-PSDelegate {
                $pso = [psobject]::AsPSObject([System.Text.StringBuilder]::new())
                $pso.Methods['Append'].Invoke(@('testing'))
                return $pso
            }

            $result = $delegate.Invoke()
            $result | Should -BeOfType System.Text.StringBuilder
            $result.ToString() | Should -Be testing
        }

        It 'throws the correct message when indexer cannot be found' {
            $expectedMsg = 'The indexer could not be determined for the type "System.Int32".'
            { New-PSDelegate { (10)[0] }} | Should -Throw $expectedMsg
        }
    }
}
