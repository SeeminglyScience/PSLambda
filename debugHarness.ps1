# Use this file to debug the module.
Import-Module -Name $PSScriptRoot\Release\PSLambda\*\PSLambda.psd1

$delegate = New-PSDelegate {
    $didFail = $false
    try {
        throw
    } catch [InvalidOperationException] {
        $didFail = $true
    }

    return $didFail
}
