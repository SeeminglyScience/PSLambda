[CmdletBinding(DefaultParameterSetName = '__AllParameterSets')]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [Parameter()]
    [string] $Framework = 'netstandard2.0',

    [ArgumentCompleter({
        Invoke-Build ? "$PSScriptRoot\PSLambda.build.ps1" |
            Where-Object Name -Like "$($args[2])*" |
            Select-Object -ExpandProperty Name
    })]
    [Parameter(Mandatory, ParameterSetName = 'Task')]
    [switch] $Task,

    [Parameter(Mandatory, ParameterSetName = 'Test')]
    [switch] $Test,

    [Parameter(Mandatory, ParameterSetName = 'Clean')]
    [switch] $Clean,

    [Parameter(Mandatory, ParameterSetName = 'Build')]
    [switch] $Build,

    [Parameter()]
    [switch] $Force,

    [Parameter()]
    [switch] $GenerateCodeCoverage
)
end {
    & "$PSScriptRoot\tools\AssertRequiredModule.ps1" InvokeBuild 5.5.2 -Force:$Force.IsPresent
    $invokeBuildSplat = @{
        File = "$PSScriptRoot/PSLambda.build.ps1"
        Force = $Force.IsPresent
        GenerateCodeCoverage = $GenerateCodeCoverage.IsPresent
        Configuration = $Configuration
        Framework = $Framework
    }

    $invokeBuildSplat['Task'] = switch ($PSCmdlet.ParameterSetName) {
        Task { $Task }
        Build { 'Build' }
        Clean { 'Clean' }
        Test { 'PrePublish' }
        default { 'PrePublish' }
    }

    Invoke-Build @invokeBuildSplat
}
