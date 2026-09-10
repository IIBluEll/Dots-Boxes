param(
    [ValidateSet('dotsandboxes','dotsandboxes_test')][string]$Database = 'dotsandboxes',
    [string]$Directory = 'C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\backups'
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath (Split-Path $Directory))) { throw 'Create a restricted backup parent directory first.' }
if (!(Test-Path -LiteralPath $Directory)) { $null = New-Item -ItemType Directory -Path $Directory }
$path = Join-Path $Directory "$Database-$(Get-Date -Format yyyyMMdd-HHmmss).dump"
$start = [Diagnostics.ProcessStartInfo]::new('docker')
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @('exec','server-db','sh','-c',"pg_dump -U `"`$POSTGRES_USER`" -d $Database --format=custom")) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
$errors = $process.StandardError.ReadToEndAsync()
$stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew)
try { $process.StandardOutput.BaseStream.CopyTo($stream) } finally { $stream.Dispose() }
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Backup failed; incomplete artifact at $path. Diagnostic output suppressed." }
$null = $errors.GetAwaiter().GetResult()
Write-Output "BACKUP=$path"
Write-Output "SHA256=$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)"
