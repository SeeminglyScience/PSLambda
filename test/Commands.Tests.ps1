using namespace System.Collections.ObjectModel
using namespace System.Diagnostics
using namespace System.Management.Automation
using namespace System.Threading.Tasks

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

    It 'lock' {
        # Need a way better way to test this
        $delegate = New-PSDelegate {
            $syncObject = [object]::new()
            $collection = [Collection[TimeSpan]]::new()
            $stopWatch = [Stopwatch]::StartNew()

            $tasks = [Collection[Task]]::new()
            for ($i = 0; $i -lt 3; $i++) {
                $tasks.Add(
                    [Task]::Run({
                        lock ($syncObject) {
                            [System.Threading.Thread]::Sleep(100)
                            $collection.Add($stopWatch.Elapsed)
                            $stopWatch.Restart()
                        }
                    }))
            }

            [Task]::WaitAll($tasks.ToArray())
            return $collection
        }

        $times = $delegate.Invoke()
        foreach ($time in $times) {
            $time.TotalMilliseconds | Should -BeGreaterThan 100
            $time.TotalMilliseconds | Should -BeLessThan 150
        }
    }
}
