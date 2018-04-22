$moduleName = 'PSLambda'
$manifestPath = "$PSScriptRoot\..\Release\$moduleName\*\$moduleName.psd1"

Describe 'module manifest values' {
    It 'can retrieve manfiest data' {
        $script:manifest = Test-ModuleManifest $manifestPath
    }
    It 'has the correct name' {
        $script:manifest.Name | Should Be $moduleName
    }
    It 'has the correct guid' {
        $script:manifest.Guid | Should Be '242ef850-1f6d-4647-acbe-26d010c4a3f5'
    }
}

