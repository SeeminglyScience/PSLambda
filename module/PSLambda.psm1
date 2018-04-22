Import-Module $PSScriptRoot\PSLambda.dll

# Can't be in psd1 as TypesToProcess loads before the assembly and it tries to resolve the
# TypeConverter that isn't in the AppDomain yet.
Update-TypeData -AppendPath $PSScriptRoot\PSLambda.types.ps1xml

Export-ModuleMember -Cmdlet New-PSDelegate
