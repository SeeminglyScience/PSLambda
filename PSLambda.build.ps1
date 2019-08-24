#requires -Module InvokeBuild -Version 5.1

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [Parameter()]
    [string] $Framework = 'netstandard2.0',

    [Parameter()]
    [switch] $GenerateCodeCoverage,

    [Parameter()]
    [switch] $Force
)

$ModuleName     = 'PSLambda'
$Culture        = 'en-US'
$ShouldAnalyze  = $false
$ShouldTest     = $true

$FailOnError = @{
    ErrorAction = [System.Management.Automation.ActionPreference]::Stop
}

$Silent = @{
    ErrorAction = [System.Management.Automation.ActionPreference]::Ignore
    WarningAction = [System.Management.Automation.ActionPreference]::Ignore
}

$Manifest       = Test-ModuleManifest -Path "$PSScriptRoot/module/$ModuleName.psd1" @Silent
$Version        = $Manifest.Version
$PowerShellPath = "$PSScriptRoot/module"
$CSharpPath     = "$PSScriptRoot/src"
$ReleasePath    = "$PSScriptRoot/Release/$ModuleName/$Version"
$DocsPath       = "$PSScriptRoot/docs"
$TestPath       = "$PSScriptRoot/test/$ModuleName.Tests"
$ToolsPath      = "$PSScriptRoot/tools"
$HasDocs        = Test-Path $DocsPath/$Culture/*.md
$HasTests       = Test-Path $TestPath/*
$IsUnix         = $PSEdition -eq 'Core' -and -not $IsWindows

function InvokeWithPSModulePath([scriptblock] $Action) {
    $oldPSModulePath = $env:PSModulePath
    try {
        $env:PSModulePath = Join-Path (Split-Path $pwsh.Path) -ChildPath 'Modules'
        . $Action
    } finally {
        $env:PSModulePath = $oldPSModulePath
    }
}

task Clean {
    if (Test-Path $ReleasePath) {
        Remove-Item $ReleasePath -Recurse
    }

    New-Item -ItemType Directory $ReleasePath | Out-Null
}

task BuildDocs -If { $HasDocs } {
    New-ExternalHelp -Path $DocsPath/$Culture -OutputPath $ReleasePath/$Culture | Out-Null
}

task AssertDependencies AssertPowerShellCore, AssertRequiredModules, AssertDotNet, AssertOpenCover

task AssertOpenCover -If { $GenerateCodeCoverage.IsPresent } {
    if ($Discovery.IsUnix) {
        Write-Warning 'Generating code coverage from .NET core is currently unsupported, disabling code coverage generation.'
        $script:GenerateCodeCoverage = $false
        return
    }

    $script:openCover = & "$ToolsPath\GetOpenCover.ps1" -Force:$Force.IsPresent
}

task AssertPowerShellCore {
    $script:pwsh = $pwsh = Get-Command pwsh @Silent
    if ($pwsh) {
        return
    }

    if ($Force.IsPresent) {
        choco install powershell-core --version 6.2.2 -y
    } else {
        choco install powershell-core --verison 6.2.2
    }

    $script:pwsh = Get-Command $env:ProgramFiles/PowerShell/6/pwsh.exe @FailOnError
}

task AssertRequiredModules {
    $assertRequiredModule = Get-Command $ToolsPath/AssertRequiredModule.ps1 @FailOnError
    & $assertRequiredModule platyPS -RequiredVersion 0.14.0 -Force:$Force.IsPresent
    & $assertRequiredModule Pester -RequiredVersion 4.8.1 -Force:$Force.IsPresent
}

task AssertDotNet {
    $script:dotnet = & $ToolsPath/GetDotNet.ps1 -Unix:$IsUnix
}

task AssertPSResGen {
    # Download the ResGen tool used by PowerShell core internally. This will need to be replaced
    # when the dotnet cli gains support for it.
    if (-not (Test-Path $ToolsPath/ResGen)) {
        New-Item -ItemType Directory $ToolsPath/ResGen | Out-Null
    }

    if (-not (Test-Path $ToolsPath/ResGen/Program.cs)) {
        $programUri = 'https://raw.githubusercontent.com/PowerShell/PowerShell/v6.2.2/src/ResGen/Program.cs'
        Invoke-WebRequest $programUri -OutFile $ToolsPath/ResGen/Program.cs @FailOnError
    }

    if (-not (Test-Path $ToolsPath/ResGen/ResGen.csproj)) {
        $projUri = 'https://raw.githubusercontent.com/PowerShell/PowerShell/v6.2.2/src/ResGen/ResGen.csproj'
        Invoke-WebRequest $projUri -OutFile $ToolsPath/ResGen/ResGen.csproj @FailOnError
    }
}

task ResGenImpl {
    Push-Location $CSharpPath/$ModuleName
    try {
        & $dotnet run --project $ToolsPath/ResGen/ResGen.csproj
    } finally {
        Pop-Location
    }
}

task BuildManaged {
    & $dotnet publish --framework $Framework --configuration $Configuration --verbosity q -nologo
}

task CopyToRelease {
    $splat = @{
        Destination = $ReleasePath
        Force = $true
        ErrorAction = [System.Management.Automation.ActionPreference]::Stop
    }

    $itemsToCopy = (
        "$ModuleName.psm1",
        "$ModuleName.psd1",
        "$ModuleName.format.ps1xml",
        "$ModuleName.types.ps1xml")

    foreach ($itemName in $itemsToCopy) {
        $splat['LiteralPath'] = Join-Path $PowerShellPath -ChildPath $itemName
        Copy-Item @splat
    }

    $itemsToCopy = (
        "$ModuleName.deps.json",
        "$ModuleName.dll",
        "$ModuleName.pdb",
        "$ModuleName.xml")

    $publishPath = Join-Path $CSharpPath -ChildPath "$ModuleName/bin/$Configuration/$Framework/publish"
    foreach ($itemName in $itemsToCopy) {
        $splat['LiteralPath'] = Join-Path $publishPath -ChildPath $itemName
        Copy-Item @splat
    }
}

task Analyze -If { $ShouldAnalyze } {
    Invoke-ScriptAnalyzer -Path $ReleasePath -Settings $PSScriptRoot/ScriptAnalyzerSettings.psd1 -Recurse
}

task DoTest {
    if ($Discovery.IsUnix) {
        $scriptString = '
            $projectPath = "{0}"
            Invoke-Pester "$projectPath" -OutputFormat NUnitXml -OutputFile "$projectPath\testresults\pester.xml"
            ' -f $PSScriptRoot
    } else {
        $scriptString = '
            Set-ExecutionPolicy Bypass -Force -Scope Process
            $projectPath = "{0}"
            Invoke-Pester "$projectPath" -OutputFormat NUnitXml -OutputFile "$projectPath\testresults\pester.xml"
            ' -f $PSScriptRoot
    }

    $encodedCommand =
        [convert]::ToBase64String(
            [System.Text.Encoding]::Unicode.GetBytes(
                $scriptString))

    $powershellCommand = 'powershell'
    if ($Discovery.IsUnix) {
        $powershellCommand = 'pwsh'
    }

    $powershell = (Get-Command $powershellCommand).Source

    if (-not $GenerateCodeCoverage.IsPresent) {
        & $powershell -NoProfile -EncodedCommand $encodedCommand
        return
    }

    # OpenCover needs full pdb's. I'm very open to suggestions for streamlining this...
    & $dotnet clean --verbosity q -nologo
    & $dotnet build --configuration $Configuration --framework netstandard2.0 --verbosity q -nologo /p:DebugType=Full

    $release = Join-Path $ReleasePath -ChildPath $ModuleName
    $coverage = Join-Path $CSharpPath -ChildPath "$ModuleName/bin/$Configuration/$Framework/$ModuleName"

    Rename-Item "$release.pdb" -NewName "$ModuleName.pdb.tmp"
    Rename-Item "$release.dll" -NewName "$ModuleName.dll.tmp"
    Copy-Item "$coverage.pdb" "$release.pdb"
    Copy-Item "$coverage.dll" "$release.dll"

    & $openCover `
        -target:$powershell `
        -register:user `
        -output:$PSScriptRoot\testresults\opencover.xml `
        -hideskipped:all `
        -filter:+[PSLambda*]* `
        -targetargs:"-NoProfile -EncodedCommand $encodedCommand"

    Remove-Item "$release.pdb"
    Remove-Item "$release.dll"
    Rename-Item "$release.pdb.tmp" -NewName "$ModuleName.pdb"
    Rename-Item "$release.dll.tmp" -NewName "$ModuleName.dll"
}

task DoInstall {
    $installBase = $Home
    if ($profile) {
        $installBase = $profile | Split-Path
    }

    $installPath = "$installBase/Modules/$ModuleName/$Version"
    if (-not (Test-Path $installPath)) {
        $null = New-Item $installPath -ItemType Directory
    }

    Copy-Item -Path $ReleasePath/* -Destination $installPath -Force -Recurse
}

task DoPublish {
    if (-not (Test-Path $env:USERPROFILE/.PSGallery/apikey.xml)) {
        throw 'Could not find PSGallery API key!'
    }

    $apiKey = (Import-Clixml $env:USERPROFILE/.PSGallery/apikey.xml).GetNetworkCredential().Password
    Publish-Module -Name $ReleasePath -NuGetApiKey $apiKey -Confirm
}

task ResGen -Jobs AssertPSResGen, ResGenImpl

task Build -Jobs Clean, AssertDependencies, ResGen, BuildManaged, CopyToRelease, BuildDocs

task Test -Jobs Build, DoTest

task PrePublish -Jobs Test

task Install -Jobs Test, DoInstall

task Publish -Jobs Test, DoPublish

task . Build
