$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'operator tests' {
    It 'DotDot' {
        (New-PSDelegate { 0..10 }).Invoke() | Should -Be (0..10)
        (New-PSDelegate { 10..0 }).Invoke() | Should -Be (10..0)
        (New-PSDelegate { -10..0 }).Invoke() | Should -Be (-10..0)
        (New-PSDelegate { -10..-20 }).Invoke() | Should -Be (-10..-20)
        (New-PSDelegate { -20..-10 }).Invoke() | Should -Be (-20..-10)
    }

    It 'Format' {
        (New-PSDelegate { 'PowerShell is {0} cool' -f 'pretty'}).Invoke() |
            Should -Be 'PowerShell is pretty cool'

        (New-PSDelegate { 'This {0} a {1} longer sentance' -f 'is', 'slightly'}).Invoke() |
            Should -Be 'This is a slightly longer sentance'
    }

    It 'Iin' {
        (New-PSDelegate { 'this' -in 'that', 'them', 'this', 'things' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -in 'that', 'them', 'things' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -in 'that', 'them', 'thIs', 'things' }).Invoke() | Should -Be $true
    }

    It 'Inotin' {
        (New-PSDelegate { 'this' -notin 'that', 'them', 'this', 'things' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -notin 'that', 'them', 'things' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -notin 'that', 'them', 'thIs', 'things' }).Invoke() | Should -Be $false
    }

    It 'Cin' {
        (New-PSDelegate { 'this' -cin 'that', 'them', 'this', 'things' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -cin 'that', 'them', 'things' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -cin 'that', 'them', 'thIs', 'things' }).Invoke() | Should -Be $false
    }

    It 'Cnotin' {
        (New-PSDelegate { 'this' -cnotin 'that', 'them', 'this', 'things' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -cnotin 'that', 'them', 'things' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -cnotin 'that', 'them', 'thIs', 'things' }).Invoke() | Should -Be $true
    }

    It 'Icontains' {
        (New-PSDelegate { 'that', 'them', 'this', 'things' -contains 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'that', 'them', 'things' -contains 'this' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'that', 'them', 'thIs', 'things' -contains 'this' }).Invoke() | Should -Be $true
    }

    It 'Inotcontains' {
        (New-PSDelegate { 'that', 'them', 'this', 'things' -notcontains 'this' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'that', 'them', 'things' -notcontains 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'that', 'them', 'thIs', 'things' -notcontains 'this' }).Invoke() | Should -Be $false
    }

    It 'Ccontains' {
        (New-PSDelegate { 'that', 'them', 'this', 'things' -ccontains 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'that', 'them', 'things' -ccontains 'this' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'that', 'them', 'thIs', 'things' -ccontains 'this' }).Invoke() | Should -Be $false
    }

    It 'Cnotcontains' {
        (New-PSDelegate { 'that', 'them', 'this', 'things' -cnotcontains 'this' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'that', 'them', 'things' -cnotcontains 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'that', 'them', 'thIs', 'things' -cnotcontains 'this' }).Invoke() | Should -Be $true
    }

    It 'Isplit' {
        (New-PSDelegate { 'here are some words' -split '\s' }).Invoke() |
            Should -Be 'here', 'are', 'some', 'words'

        (New-PSDelegate { 'this' -split '(\w)' }).Invoke() | Should -Be '', 't', '', 'h', '', 'i', '', 's', ''
        (New-PSDelegate { 'thIs' -split '([a-z])' }).Invoke() | Should -Be '', 't', '', 'h', '', 'I', '', 's', ''
    }

    It 'Csplit' {
        (New-PSDelegate { 'here are some words' -csplit '\s' }).Invoke() |
            Should -Be 'here', 'are', 'some', 'words'

        (New-PSDelegate { 'this' -csplit '(\w)' }).Invoke() | Should -Be '', 't', '', 'h', '', 'i', '', 's', ''
        (New-PSDelegate { 'thIs' -csplit '([a-z])' }).Invoke() | Should -Be '', 't', '', 'h', 'I', 's', ''
    }

    It 'Ilike' {
        (New-PSDelegate { 'this' -like 't*s' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'that' -like 't*s' }).Invoke() | Should -Be $false
        (New-PSDelegate { 01243 -like '*4*' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'That' -like 'that' }).Invoke() | Should -Be $true
    }

    It 'Clike' {
        (New-PSDelegate { 'this' -clike 't*s' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'that' -clike 't*s' }).Invoke() | Should -Be $false
        (New-PSDelegate { 01243 -clike '*4*' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'That' -clike 'that' }).Invoke() | Should -Be $false
    }

    It 'Inotlike' {
        (New-PSDelegate { 'this' -notlike 't*s' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'that' -notlike 't*s' }).Invoke() | Should -Be $true
        (New-PSDelegate { 01243 -notlike '*4*' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'That' -notlike 'that' }).Invoke() | Should -Be $false
    }

    It 'Cnotlike' {
        (New-PSDelegate { 'this' -cnotlike 't*s' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'that' -cnotlike 't*s' }).Invoke() | Should -Be $true
        (New-PSDelegate { 01243 -cnotlike '*4*' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'That' -cnotlike 'that' }).Invoke() | Should -Be $true
    }

    It 'Imatch' {
        (New-PSDelegate { 'this' -match 'This' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -match 't[hi]{2}s' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -match '\w+' }).Invoke() | Should -Be $true
    }

    It 'Cmatch' {
        (New-PSDelegate { 'this' -cmatch 'This' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -cmatch 't[hi]{2}s' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -cmatch '\w+' }).Invoke() | Should -Be $true
    }

    It 'Inotmatch' {
        (New-PSDelegate { 'this' -notmatch 'This' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -notmatch 't[hi]{2}s' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -notmatch '\w+' }).Invoke() | Should -Be $false
    }

    It 'Cnotmatch' {
        (New-PSDelegate { 'this' -cnotmatch 'This' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -cnotmatch 't[hi]{2}s' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -cnotmatch '\w+' }).Invoke() | Should -Be $false
    }

    It 'Igt' {
        (New-PSDelegate { 1 -gt 0 }).Invoke() | Should -Be $true
        (New-PSDelegate { 0 -gt 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -gt 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -gt 'thIs' }).Invoke() | Should -Be $false
    }

    It 'Cgt' {
        (New-PSDelegate { 1 -cgt 0 }).Invoke() | Should -Be $true
        (New-PSDelegate { 0 -cgt 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -cgt 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 'thIs' -cgt 'this' }).Invoke() | Should -Be $true
    }

    It 'Ige' {
        (New-PSDelegate { 0 -ge 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -ge 0 }).Invoke() | Should -Be $true
        (New-PSDelegate { 1 -ge 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 'thIs' -ge 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -ge 'thIs' }).Invoke() | Should -Be $true
    }

    It 'Cge' {
        (New-PSDelegate { 0 -cge 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -cge 0 }).Invoke() | Should -Be $true
        (New-PSDelegate { 1 -cge 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 'thIs' -cge 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -cge 'thIs' }).Invoke() | Should -Be $false
    }

    It 'Ilt' {
        (New-PSDelegate { 0 -lt 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 1 -lt 0 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -lt 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 'thIs' -lt 'this' }).Invoke() | Should -Be $false
    }

    It 'Clt' {
        (New-PSDelegate { 0 -clt 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 1 -clt 0 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -clt 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -clt 'thIs' }).Invoke() | Should -Be $true
    }

    It 'Ile' {
        (New-PSDelegate { 0 -le 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 1 -le 0 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -le 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 'thIs' -le 'this' }).Invoke() | Should -Be $true
        (New-PSDelegate { 'this' -le 'thIs' }).Invoke() | Should -Be $true
    }

    It 'Cle' {
        (New-PSDelegate { 0 -cle 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 1 -cle 0 }).Invoke() | Should -Be $false
        (New-PSDelegate { 1 -cle 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 'thIs' -cle 'this' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -cle 'thIs' }).Invoke() | Should -Be $true
    }

    It 'Ieq' {
        (New-PSDelegate { 1 -eq 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { '1' -eq 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 0 -eq 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { '0' -eq 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -eq 'that' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -eq 'This' }).Invoke() | Should -Be $true
    }

    It 'Ceq' {
        (New-PSDelegate { 1 -ceq 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { '1' -ceq 1 }).Invoke() | Should -Be $true
        (New-PSDelegate { 0 -ceq 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { '0' -ceq 1 }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -ceq 'that' }).Invoke() | Should -Be $false
        (New-PSDelegate { 'this' -ceq 'This' }).Invoke() | Should -Be $false
    }

    It 'Join' {
        (New-PSDelegate { 0, 2, 4 -join '' }).Invoke() | Should -Be '024'
        (New-PSDelegate { ([type], [string], [int]) -join '-'}).Invoke() | Should -Be 'type-string-int'
    }

    It 'Ireplace' {
        (New-PSDelegate { 'Test string' -replace 't', 'w' }).Invoke() | Should -Be 'wesw swring'
        (New-PSDelegate { 'Test string' -replace '^[A-Z]+', '-$0-' }).Invoke() | Should -Be '-Test- string'
    }

    It 'Creplace' {
        (New-PSDelegate { 'Test string' -creplace 't', 'w' }).Invoke() | Should -Be 'Tesw swring'
        (New-PSDelegate { 'Test string' -creplace '^[A-Z]+', '-$0-' }).Invoke() | Should -Be '-T-est string'
    }

    It 'And' {
        (New-PSDelegate { 'string' -and $true }).Invoke() | Should -Be $true
        (New-PSDelegate { 'string' -and $false }).Invoke() | Should -Be $false
        (New-PSDelegate { $false -and $false }).Invoke() | Should -Be $false
    }

    It 'Or' {
        (New-PSDelegate { 'string' -or $true }).Invoke() | Should -Be $true
        (New-PSDelegate { 'string' -or $false }).Invoke() | Should -Be $true
        (New-PSDelegate { $false -or $false }).Invoke() | Should -Be $false
        (New-PSDelegate { $false -or $true }).Invoke() | Should -Be $true
    }

    It 'Not' {
        (New-PSDelegate { -not $false }).Invoke() | Should -Be $true
        (New-PSDelegate { -not $true }).Invoke() | Should -Be $false
    }

    It 'Bor' {
        (New-PSDelegate { [Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::Public }).Invoke() |
            Should -Be ([Reflection.BindingFlags]'Instance, Public')
    }

    It 'Band' {
        $flags = [Reflection.BindingFlags]'Instance, Public'
        (New-PSDelegate { $flags -band [Reflection.BindingFlags]::Public }).Invoke() |
            Should -Be ([Reflection.BindingFlags]'Public')
    }

    It 'As' {
        (New-PSDelegate { 'string' -as [type] }).Invoke() | Should -Be ([string])
        (New-PSDelegate { 'does_not_exist' -as [type] }).Invoke() | Should -Be $null
        { New-PSDelegate { 'this' -as 'that' }} | Should -Throw '-as or -is expression with non-literal type.'
    }

    It 'Is' {
        (New-PSDelegate { 'string' -is [string] }).Invoke() | Should -Be $true
        (New-PSDelegate { 10 -is [int] }).Invoke() | Should -Be $true
        (New-PSDelegate { 'string' -is [int] }).Invoke() | Should -Be $false
    }

    It 'Isnot' {
        (New-PSDelegate { 'string' -isnot [string] }).Invoke() | Should -Be $false
        (New-PSDelegate { 10 -isnot [int] }).Invoke() | Should -Be $false
        (New-PSDelegate { 'string' -isnot [int] }).Invoke() | Should -Be $true
    }

    It 'Add' {
        (New-PSDelegate { 2 + 1 }).Invoke() | Should -Be 3
    }

    It 'Subtract' {
        (New-PSDelegate { 2 - 1 }).Invoke() | Should -Be 1
    }

    It 'Divide' {
        (New-PSDelegate { 4 / 2 }).Invoke() | Should -Be 2
    }

    It 'Multiply' {
        (New-PSDelegate { 4 * 2 }).Invoke() | Should -Be 8
    }

    It 'Rem' {
        (New-PSDelegate { 4 % 3 }).Invoke() | Should -Be 1
    }
}
