param([string]$Dotnet='dotnet',[string]$Makensis='makensis')
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root 'src\FlydigiKeyMapper.csproj'
$version=([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$app=Join-Path $root 'artifacts\app';$dep=Join-Path $root 'artifacts\dependencies'
New-Item -ItemType Directory $app,$dep -Force | Out-Null
function Download([string[]]$Urls,[string]$Path,[string]$Hash){
 if((Test-Path $Path)-and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Hash){return}
 foreach($url in $Urls){try{Invoke-WebRequest $url -OutFile $Path;if((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Hash){return}}catch{Write-Warning $_}}
 throw "Could not download a verified dependency: $Path"
}
Download @('https://github.com/desowin/usbpcap/releases/download/1.5.4.0/USBPcapSetup-1.5.4.0.exe') (Join-Path $dep 'USBPcapSetup-1.5.4.0.exe') '87A7EDF9BBBCF07B5F4373D9A192A6770D2FF3ADD7AA1E276E82E38582CCB622'
Download @('https://downloads.sourceforge.net/project/usbpcap/USBPcap/1.5.4.0/usbpcap-src-incl-pdb-1.5.4.0.7z','https://netix.dl.sourceforge.net/project/usbpcap/USBPcap/1.5.4.0/usbpcap-src-incl-pdb-1.5.4.0.7z') (Join-Path $dep 'usbpcap-src-incl-pdb-1.5.4.0.7z') '3A66CF03425F5860FF8ACD64D11CC0BB50669C7F449754F47788011259A5B15E'
if((Get-AuthenticodeSignature (Join-Path $dep 'USBPcapSetup-1.5.4.0.exe')).Status -ne 'Valid'){throw 'USBPcap signature verification failed'}
$seven=(Get-Command 7z.exe -ErrorAction SilentlyContinue).Source;if(-not$seven){$seven=Join-Path $env:ProgramFiles '7-Zip\7z.exe'}
& $seven e (Join-Path $dep 'usbpcap-src-incl-pdb-1.5.4.0.7z') 'usbpcap\nsis\bsd-2clause.txt' 'usbpcap\nsis\gpl-2.0.txt' ('-o'+$dep) -y | Out-Null
if($LASTEXITCODE -ne 0){throw 'USBPcap license extraction failed'}
((Get-Content (Join-Path $dep 'bsd-2clause.txt') -Raw)+"`r`n"+(Get-Content (Join-Path $dep 'gpl-2.0.txt') -Raw)) | Set-Content (Join-Path $dep 'USBPcap-LICENSES.txt')
& $Dotnet run --project (Join-Path $root 'tests\Tests.csproj') -c Release;if($LASTEXITCODE -ne 0){throw 'Tests failed'}
& $Dotnet publish $project -c Release -o $app;if($LASTEXITCODE -ne 0){throw 'Publish failed'}
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$vcvars=if($vs){Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'}else{(Get-Item "$env:ProgramFiles\Microsoft Visual Studio\*\*\VC\Auxiliary\Build\vcvars64.bat" -ErrorAction SilentlyContinue | Select-Object -First 1).FullName}
if(-not$vcvars){throw 'MSVC C++ tools and Windows SDK are required'}
$native=Join-Path $root 'artifacts\build-native.cmd'
@"
@call "$vcvars" >nul
cl /nologo /LD /MT /EHsc /std:c++17 "$root\src\VendorBridge.cpp" "$root\src\UsbDiscovery.cpp" /Fe:"$app\VendorBridge.dll" /Fo:"$root\artifacts\\" /link windowsapp.lib user32.lib
"@ | Set-Content -LiteralPath $native -Encoding ascii
& cmd.exe /c $native;if($LASTEXITCODE -ne 0){throw 'Native build failed'}
# Copy runtime licensing from the exact restored NuGet runtime packs.
$nuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget\packages'}
$assets=Get-Content (Join-Path $root 'src\obj\project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
foreach($entry in $assets.libraries.Keys | Where-Object {$_ -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64/'}){
 $package=$entry.ToLowerInvariant();$packageRoot=Join-Path $nuget $package
 foreach($license in Get-ChildItem -LiteralPath $packageRoot -File | Where-Object {$_.Name -match '^LICENSE|^THIRD-PARTY-NOTICES'}){Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $app (($package.Split('/')[0])+'-'+$license.Name)) -Force}
}

# Generate an exact uninstall manifest; never recursively delete the installation directory.
$files=Get-ChildItem -LiteralPath $app -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/]VendorRuntimeCache[\\/]' -and $_.Extension -notin '.pdb','.lib','.exp' -and $_.Name -ne 'settings.json' -and $_.Name -notlike '*-preview.png'}
$remove=@($files | ForEach-Object {'Delete "$INSTDIR\'+[IO.Path]::GetRelativePath($app,$_.FullName)+'"'})
$remove+=@(Get-ChildItem -LiteralPath $app -Directory -Recurse | Sort-Object {$_.FullName.Length} -Descending | ForEach-Object {'RMDir "$INSTDIR\'+[IO.Path]::GetRelativePath($app,$_.FullName)+'"'})
$remove | Set-Content (Join-Path $root 'installer\uninstall-files.nsh')
& $Makensis ('/DVERSION='+$version) (Join-Path $root 'installer\setup.nsi');if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$artifacts=Join-Path $root 'artifacts'
Copy-Item (Join-Path $dep 'usbpcap-src-incl-pdb-1.5.4.0.7z') $artifacts -Force
Copy-Item (Join-Path $dep 'USBPcap-LICENSES.txt') $artifacts -Force
$release=@(Get-Item (Join-Path $artifacts "FlydigiKeyMapper-Setup-$version-x64.exe"), (Join-Path $artifacts 'usbpcap-src-incl-pdb-1.5.4.0.7z'), (Join-Path $artifacts 'USBPcap-LICENSES.txt'))
$release | ForEach-Object {((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_.Name} | Set-Content (Join-Path $artifacts 'SHA256SUMS.txt')
Write-Host "Release $version built in $artifacts"




