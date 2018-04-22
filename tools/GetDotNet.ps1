[CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
param(
    [string] $Version = '2.1.4',
    [switch] $Unix,
    [switch] $Force
)
end {
    $TARGET_FOLDER = "$PSScriptRoot/dotnet"

    if (($dotnet = Get-Command dotnet -ea 0) -and [version](& $dotnet --version) -ge [version]$Version) {
        return $dotnet
    }

    $executable = 'dotnet.exe'
    if ($Unix.IsPresent) {
        $executable = 'dotnet'
    }

    if ($dotnet = Get-Command $TARGET_FOLDER/$executable -ea 0) {
        if ([version]($found = & $dotnet --version) -ge [version]$Version) {
            return $dotnet
        }
        Write-Host -ForegroundColor Yellow Found dotnet $found but require $Version, replacing...
        if ($Force.IsPresent -or $PSCmdlet.ShouldProcess($TARGET_FOLDER, 'Remove-Item')) {
            Remove-Item $TARGET_FOLDER -Recurse
        } else {
            throw 'Existing version of the dotnet cli must be removed before it can be redownloaded.'
        }

        $dotnet = $null
    }

    Write-Host -ForegroundColor Green Downloading dotnet version $Version

    if ($Force.IsPresent -or $PSCmdlet.ShouldProcess("dotnet $Version", 'Install')) {
        if ($Unix.IsPresent) {
            $uri = "https://raw.githubusercontent.com/dotnet/cli/v2.0.0/scripts/obtain/dotnet-install.sh"
            $installerPath = [System.IO.Path]::GetTempPath() + 'dotnet-install.sh'
            $scriptText = [System.Net.WebClient]::new().DownloadString($uri)
            Set-Content $installerPath -Value $scriptText -Encoding UTF8
            $installer = { param($Version, $InstallDir) & (Get-Command bash) $installerPath -Version $Version -InstallDir $InstallDir }
        } else {
            $uri = "https://raw.githubusercontent.com/dotnet/cli/v2.0.0/scripts/obtain/dotnet-install.ps1"
            $scriptText = [System.Net.WebClient]::new().DownloadString($uri)

            # Stop the official script from hard exiting at times...
            $safeScriptText = $scriptText -replace 'exit 0', 'return'
            $installer = [scriptblock]::Create($safeScriptText)
        }

        $null = & $installer -Version $Version -InstallDir $TARGET_FOLDER
    } else {
        throw 'The dotnet cli is required to build this project.'
    }

    return Get-Command $TARGET_FOLDER/$executable
}
