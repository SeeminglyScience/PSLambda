#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [Parameter()]
    [switch] $GenerateCodeCoverage,

    [Parameter()]
    [switch] $Force
)

$moduleName = 'PSLambda'
$manifest = Test-ModuleManifest -Path $PSScriptRoot\module\$moduleName.psd1 -ErrorAction Ignore -WarningAction Ignore

$script:Settings = @{
    Name          = $moduleName
    Manifest      = $manifest
    Version       = $manifest.Version
    ShouldAnalyze = $false
    ShouldTest    = $true
}

$script:Folders  = @{
    PowerShell = "$PSScriptRoot\module"
    Release    = '{0}\Release\{1}\{2}' -f $PSScriptRoot, $moduleName, $manifest.Version
    Build      = '{0}\src\{1}\bin\{2}' -f $PSScriptRoot, $moduleName, $Configuration
    Docs       = "$PSScriptRoot\docs"
    Test       = "$PSScriptRoot\test"
    PesterCC   = "$PSScriptRoot\*.psm1", "$PSScriptRoot\Public\*.ps1", "$PSScriptRoot\Private\*.ps1"
}

$script:Discovery = @{
    HasDocs       = Test-Path ('{0}\{1}\*.md' -f $Folders.Docs, $PSCulture)
    HasTests      = Test-Path ('{0}\*.Tests.ps1' -f $Folders.Test)
}

$tools = "$PSScriptRoot\tools"
$script:GetDotNet = Get-Command $tools\GetDotNet.ps1
$script:AssertModule = Get-Command $tools\AssertRequiredModule.ps1
$script:GetOpenCover = Get-Command $tools\GetOpenCover.ps1

task AssertDotNet {
    $script:dotnet = & $GetDotNet -Unix:$Discovery.IsUnix -Force:$Force.IsPresent
}

task AssertOpenCover -If { $GenerateCodeCoverage.IsPresent } {
    if ($Discovery.IsUnix) {
        Write-Warning 'Generating code coverage from .NET core is currently unsupported, disabling code coverage generation.'
        $script:GenerateCodeCoverage = $false
        return
    }

    $script:openCover = & $GetOpenCover -Force:$Force.IsPresent
}

task AssertRequiredModules {
    & $AssertModule Pester 4.3.1 -Force:$Force.IsPresent
    & $AssertModule InvokeBuild 5.4.1 -Force:$Force.IsPresent
    & $AssertModule platyPS 0.9.0 -Force:$Force.IsPresent
}

task Clean {
    $releaseFolder = $Folders.Release
    if (Test-Path $releaseFolder) {
        Remove-Item $releaseFolder -Recurse
    }

    if (Test-Path $PSScriptRoot\testresults) {
        Remove-Item $PSScriptRoot\testresults -Recurse
    }

    New-Item -ItemType Directory $releaseFolder | Out-Null
    New-Item -ItemType Directory $PSScriptRoot\testresults | Out-Null

}

task BuildDocs -If { $Discovery.HasDocs } {
    $output = '{0}\{1}' -f $Folders.Release, $PSCulture
    $null = New-ExternalHelp -Path $PSScriptRoot\docs\$PSCulture -OutputPath $output
}

task AssertPSResGen {
    # Download the ResGen tool used by PowerShell core internally. This will need to be replaced
    # when the dotnet cli gains support for it.
    # The SHA in the uri's are for the 6.0.2 release commit.
    if (-not (Test-Path $PSScriptRoot/tools/ResGen)) {
        New-Item -ItemType Directory $PSScriptRoot/tools/ResGen | Out-Null
    }

    if (-not (Test-Path $PSScriptRoot/tools/ResGen/Program.cs)) {
        $programUri = 'https://raw.githubusercontent.com/PowerShell/PowerShell/36b71ba39e36be3b86854b3551ef9f8e2a1de5cc/src/ResGen/Program.cs'
        Invoke-WebRequest $programUri -OutFile $PSScriptRoot/tools/ResGen/Program.cs -ErrorAction Stop
    }

    if (-not (Test-Path $PSScriptRoot/tools/ResGen/ResGen.csproj)) {
        $projUri = 'https://raw.githubusercontent.com/PowerShell/PowerShell/36b71ba39e36be3b86854b3551ef9f8e2a1de5cc/src/ResGen/ResGen.csproj'
        Invoke-WebRequest $projUri -OutFile $PSScriptRoot/tools/ResGen/ResGen.csproj -ErrorAction Stop
    }
}

task ResGenImpl {
    Push-Location $PSScriptRoot/src/PSLambda
    try {
        & $dotnet run --project $PSScriptRoot/tools/ResGen/ResGen.csproj --verbosity q -nologo
    } finally {
        Pop-Location
    }
}

task BuildManaged {
    & $dotnet build --framework netstandard2.0 --configuration $Configuration --verbosity q -nologo
}

task CopyToRelease {
    $releaseFolder = $Folders.Release
    Copy-Item $PSScriptRoot/src/PSLambda/bin/$Configuration/netstandard2.0/PSLambda.* -Destination $releaseFolder
    Copy-Item $PSScriptRoot/module/PSLambda.psd1 $releaseFolder
    Copy-Item $PSScriptRoot/module/PSLambda.psm1 $releaseFolder
    Copy-Item $PSScriptRoot/module/PSLambda.types.ps1xml $releaseFolder
    Copy-Item $PSScriptRoot/module/PSLambda.format.ps1xml $releaseFolder
}

task Analyze -If { $Settings.ShouldAnalyze } {
    Invoke-ScriptAnalyzer -Path $Folders.Release -Settings $PSScriptRoot\ScriptAnalyzerSettings.psd1 -Recurse
}

task DoTest -If { $Discovery.HasTests -and $Settings.ShouldTest } {
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

    if ($GenerateCodeCoverage.IsPresent) {
        $script:openCover = . $PSScriptRoot\tools\GetOpenCover.ps1
        # OpenCover needs full pdb's. I'm very open to suggestions for streamlining this...
        & $dotnet clean --verbosity q -nologo
        & $dotnet build --configuration $Configuration --framework netstandard2.0 --verbosity q -nologo /p:DebugType=Full

        $moduleName = $Settings.Name
        $release = '{0}\{1}' -f $Folders.Release, $moduleName
        $coverage = '{0}\netstandard2.0\{1}' -f $Folders.Build, $moduleName

        Rename-Item "$release.pdb" -NewName "$moduleName.pdb.tmp"
        Rename-Item "$release.dll" -NewName "$moduleName.dll.tmp"
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
        Rename-Item "$release.pdb.tmp" -NewName "$moduleName.pdb"
        Rename-Item "$release.dll.tmp" -NewName "$moduleName.dll"
    } else {
        & $powershell -NoProfile -EncodedCommand $encodedCommand
    }
}

task DoInstall {
    $installBase = $Home
    if ($profile) { $installBase = $profile | Split-Path }
    $installPath = '{0}\Modules\{1}\{2}' -f $installBase, $Settings.Name, $Settings.Version

    if (-not (Test-Path $installPath)) {
        $null = New-Item $installPath -ItemType Directory
    }

    Copy-Item -Path ('{0}\*' -f $Folders.Release) -Destination $installPath -Force -Recurse
}

task DoPublish {
    if (-not (Test-Path $env:USERPROFILE\.PSGallery\apikey.xml)) {
        throw 'Could not find PSGallery API key!'
    }

    $apiKey = (Import-Clixml $env:USERPROFILE\.PSGallery\apikey.xml).GetNetworkCredential().Password
    Publish-Module -Name $Folders.Release -NuGetApiKey $apiKey -Confirm
}

task AssertDevDependencies -Jobs AssertDotNet, AssertOpenCover, AssertRequiredModules

task ResGen -Jobs AssertPSResGen, ResGenImpl

task Build -Jobs AssertDevDependencies, Clean, ResGen, BuildManaged, CopyToRelease, BuildDocs

Task Test -Jobs AssertDevDependencies, DoTest

task PreRelease -Jobs Build, Analyze, DoTest

task Install -Jobs PreRelease, DoInstall

task Publish -Jobs PreRelease, DoPublish

task . Build

