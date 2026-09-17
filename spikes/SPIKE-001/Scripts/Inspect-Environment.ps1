[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host '=== OS ==='
$osRegistry = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
[pscustomobject]@{
    ProductName = $osRegistry.ProductName
    DisplayVersion = $osRegistry.DisplayVersion
    CurrentBuild = $osRegistry.CurrentBuild
    UBR = $osRegistry.UBR
    RuntimeVersion = [System.Environment]::OSVersion.VersionString
    ProcessArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
}

Write-Host '=== PowerShell ==='
$PSVersionTable | Select-Object PSVersion, PSEdition, OS

Write-Host '=== .NET ==='
dotnet --info
if ($LASTEXITCODE -ne 0) {
    throw "dotnet --info failed with exit code $LASTEXITCODE."
}

Write-Host '=== Windows App Runtime packages visible to the current user ==='
$runtimePackages = Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime*' -ErrorAction SilentlyContinue |
    Select-Object Name, Version, Architecture, Status, PackageFullName
if ($null -eq $runtimePackages) {
    Write-Host 'No Microsoft.WindowsAppRuntime packages were returned for the current user.'
}
else {
    $runtimePackages | Format-Table -AutoSize
}

Write-Host '=== Process elevation ==='
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
[pscustomobject]@{
    User = $identity.Name
    IsAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    ProcessArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
}
