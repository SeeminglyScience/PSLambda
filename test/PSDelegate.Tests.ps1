using namespace System.Collections.Generic
using namespace System.Threading.Tasks

$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'Delegate conversion' {
    It 'can handle Task.Run' {
        & {
            $myVar = 'test'
            [Task]::Run([action][psdelegate]{ $myVar = 'newValue' }).GetAwaiter().GetResult()
            $myVar | Should -Be 'newValue'
        }
    }

    It 'can infer parameters from conversion' {
        $originalObject = [object]::new()
        $task = [Task[psobject]]::Factory.StartNew(
            [psdelegate]{ ($state) => { [PSObject]::AsPSObject($state) }},
            $originalObject)

        $newObject = $task.GetAwaiter().GetResult()
        $originalObject.GetHashCode() | Should -Be $newObject.psobject.BaseObject.GetHashCode()
    }

    It 'retains locals from origin scope' {
        $delegate = & {
            $myVar = 'goodValue'
            [psdelegate]{ $myVar }
        }

        $myVar = 'badValue'
        $delegate.Invoke() | Should -Be 'goodValue'
    }

    It 'changes locals from origin scope' {
        & {
            $myVar = 'oldValue'
            $delegate = [psdelegate]{ $myVar = 'newValue' }
            & {
                $myVar = 'newScopeValue'
                $delegate.Invoke()
                $myVar | Should -Be 'newScopeValue'
            }

            $myVar | Should -Be 'newValue'
        }
    }

    It 'can be converted inside existing delegate with convert expression' {
        $task = [Task[int]]::Factory.StartNew([psdelegate]{
            $total = 0
            $list = [List[int]]::new(0..5)
            $list.ForEach([action[int]]{ ($i) => { $total = $total + $i }})
            return $total
        })

        $task.GetAwaiter().GetResult() | Should -Be 15
    }
}
