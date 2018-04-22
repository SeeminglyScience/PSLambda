$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Import-Module $manifestPath -Force

Describe 'parse error tests' {
    It 'unsupported AST type <Ast> throws a parse exception' -TestCases (
            @{ Ast = 'FunctionDefinitionAst'; Test = { function Test {} }},
            @{ Ast = 'TypeDefinitionAst'; Test = { class Test {} }},
            @{ Ast = 'CommandAst'; Test = { Get-ChildItem }},
            @{ Ast = 'DataStatementAst'; Test = { data {} }},
            @{ Ast = 'FileRedirectionAst'; Test = { $ExecutionContext > 'test' }},
            @{ Ast = 'MergingRedirectionAst'; Test = { $ExecutionContext 2>&1 }},
            @{ Ast = 'TrapStatementAst'; Test = { trap {} end{ $ExecutionContext }}},
            @{ Ast = 'AttributeAst'; Test = { [Parameter()]$thing = $null }},
            @{ Ast = 'UsingExpressionAst'; Test = { $using:ExecutionContext }}
    ) -Test {
        param($Test)
        end {
            { New-PSDelegate $Test } | Should -Throw 'Unable to compile ScriptBlock due to an unsupported element'
        }
    }
}
