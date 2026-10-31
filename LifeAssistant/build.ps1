$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Missing .NET Framework 4 compiler (64-bit Windows required).' }
$refs = @('System.dll', 'System.Core.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + (Join-Path $PSScriptRoot 'LifeAssistant.exe')), ('/resource:' + (Join-Path $PSScriptRoot 'MainWindow.xaml') + ',MainWindow.xaml'))
foreach ($reference in $refs) { $arguments += '/reference:' + (Join-Path $framework $reference) }
foreach ($source in @('App.cs', 'TaskStore.cs', 'StoreTests.cs')) { $arguments += Join-Path $PSScriptRoot $source }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Build succeeded: LifeAssistant.exe'
