#
# Module manifest for module 'PSLambda'
#
# Generated by: Patrick M. Meinecke
#
# Generated on: 4/20/2018
#

@{

# Script module or binary module file associated with this manifest.
RootModule = 'PSLambda.psm1'

# Version number of this module.
ModuleVersion = '0.1.0'

# ID used to uniquely identify this module
GUID = '242ef850-1f6d-4647-acbe-26d010c4a3f5'

# Author of this module
Author = 'Patrick M. Meinecke'

# Company or vendor of this module
CompanyName = 'Community'

# Copyright statement for this module
Copyright = '(c) 2018 Patrick M. Meinecke. All rights reserved.'

# Description of the functionality provided by this module
Description = 'A runtime delegate compiler for PowerShell ScriptBlock objects.'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = '5.1'

# Minimum version of Microsoft .NET Framework required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
DotNetFrameworkVersion = '4.7.1'

# Minimum version of the common language runtime (CLR) required by this module. This prerequisite is valid for the PowerShell Desktop edition only.
CLRVersion = '4.0'

# Processor architecture (None, X86, Amd64) required by this module
ProcessorArchitecture = 'None'

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = 'PSLambda.format.ps1xml'

# Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
FunctionsToExport = @()

# Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
CmdletsToExport = 'New-PSDelegate'

# Variables to export from this module
VariablesToExport = @()

# Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
AliasesToExport = @()

# List of all files packaged with this module
FileList = 'PSLambda.psd1',
           'PSLambda.psm1',
           'PSLambda.types.ps1xml',
           'PSLambda.format.ps1xml',
           'PSLambda.dll',
           'PSLambda.pdb',
           'PSLambda.deps.json',
           'PSLambda.xml'

# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{

        # Tags applied to this module. These help with module discovery in online galleries.
        Tags = @()

        # A URL to the license for this module.
        LicenseUri = 'https://github.com/SeeminglyScience/PSLambda/blob/master/LICENSE'

        # A URL to the main website for this project.
        ProjectUri = 'https://github.com/SeeminglyScience/PSLambda'

        # A URL to an icon representing this module.
        # IconUri = ''

        # ReleaseNotes of this module
        # ReleaseNotes = ''

    } # End of PSData hashtable

} # End of PrivateData hashtable

}



