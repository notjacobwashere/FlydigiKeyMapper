# Only official Microsoft Store delivery is used for Flydigi's proprietary component.
$ErrorActionPreference = 'Stop'
function Test-FlydigiInstalled {
    $packages = @(Get-AppxPackage -Name 'Flydigi.*' | Where-Object { $_.Architecture -eq 'X64' })
    return [bool]($packages | Where-Object { Test-Path (Join-Path $_.InstallLocation 'D4XGaming.Devices.dll') })
}
if (Test-FlydigiInstalled) { exit 0 }
$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if ($winget) {
    # Do not accept license/source agreements on the user's behalf.
    & $winget.Source install --id 9NB9DQG1BN6P --exact --source msstore --disable-interactivity
    if (Test-FlydigiInstalled) { exit 0 }
}
Add-Type -AssemblyName System.Windows.Forms
Start-Process 'ms-windows-store://pdp/?ProductId=9NB9DQG1BN6P'
do {
    $answer = [System.Windows.Forms.MessageBox]::Show(
        'Install Flydigi Space Station for Xbox in the Microsoft Store window, then click Retry. Microsoft Store may require your approval or sign-in. Cancel skips this dependency; the mapper cannot read buttons until it is installed.',
        'Flydigi dependency', 'RetryCancel', 'Information')
    if (Test-FlydigiInstalled) { exit 0 }
} while ($answer -eq 'Retry')
exit 1
