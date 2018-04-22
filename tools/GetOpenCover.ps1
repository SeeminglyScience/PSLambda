[CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
param(
    [string] $Version = '4.6.519',
    [switch] $Force
)
end {
    function GetVersionNumber {
        param([System.Management.Automation.CommandInfo] $Command)
        end {
            return (& $Command -version) `
                -replace 'OpenCover version ' `
                -replace '\.0$'
        }
    }

    $TARGET_FOLDER  = "$PSScriptRoot\opencover"
    $TARGET_ARCHIVE = "$PSScriptRoot\opencover.zip"
    $TARGET_NAME    = 'OpenCover.Console.exe'

    $ErrorActionPreference = 'Stop'

    if ($openCover = Get-Command $TARGET_FOLDER\$TARGET_NAME -ea 0) {
        if (($found = GetVersionNumber $openCover) -eq $Version) {
            return $openCover
        }

        Write-Host -ForegroundColor Yellow Found OpenCover $found but require $Version, replacing...

        if ($Force.IsPresent -or $PSCmdlet.ShouldProcess($TARGET_FOLDER, 'Remove-Item')) {
            Remove-Item $TARGET_FOLDER -Recurse
        } else {
            throw 'Existing version of OpenCover must be removed before it can be redownloaded.'
        }
    }
    Write-Host -ForegroundColor Green Downloading OpenCover version $Version

    $url = "https://github.com/OpenCover/opencover/releases/download/$Version/opencover.$Version.zip"
    if ($Force.IsPresent -or $PSCmdlet.ShouldProcess($url, 'Invoke-WebRequest')) {
        $oldSecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol
        try {
            [System.Net.ServicePointManager]::SecurityProtocol = 'Tls, Tls11, Tls12'
            Invoke-WebRequest $url -OutFile $TARGET_ARCHIVE
        } finally {
            [System.Net.ServicePointManager]::SecurityProtocol = $oldSecurityProtocol
        }

        Expand-Archive $TARGET_ARCHIVE -DestinationPath $TARGET_FOLDER -Force
        Remove-Item $TARGET_ARCHIVE
    } else {
        throw 'OpenCover is required to generate code coverage.'
    }


    return Get-Command $TARGET_FOLDER\$TARGET_NAME

}
