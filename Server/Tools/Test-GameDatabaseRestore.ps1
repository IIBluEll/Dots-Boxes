param([Parameter(Mandatory)][string]$BackupPath)
$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $BackupPath).Path
if ([IO.Path]::GetExtension($resolved) -ne '.dump') { throw 'Expected a PostgreSQL custom-format .dump file.' }
$restoreDatabase = 'dotsandboxes_restorecheck_' + (Get-Date -Format yyyyMMddHHmmss)
$sql = @"
CREATE DATABASE $restoreDatabase OWNER dotsandboxes_test_migrator TEMPLATE template0 ENCODING 'UTF8';
REVOKE ALL ON DATABASE $restoreDatabase FROM PUBLIC;
"@
$sql | docker exec -i server-db sh -c 'psql -X -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres'
if ($LASTEXITCODE -ne 0) { throw 'Restore DB creation failed; stop and inspect.' }
$start = [Diagnostics.ProcessStartInfo]::new('docker')
$start.UseShellExecute = $false
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
foreach ($argument in @('exec','-i','server-db','sh','-c',"pg_restore -U `"`$POSTGRES_USER`" --role=dotsandboxes_test_migrator --no-owner --no-privileges --exit-on-error -d $restoreDatabase")) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
$errors = $process.StandardError.ReadToEndAsync()
$output = $process.StandardOutput.ReadToEndAsync()
$stream = [IO.File]::OpenRead($resolved)
try { $stream.CopyTo($process.StandardInput.BaseStream) } finally { $stream.Dispose(); $process.StandardInput.Close() }
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Restore failed in new DB $restoreDatabase; original DB untouched. Diagnostics suppressed." }
$null = $errors.GetAwaiter().GetResult(); $null = $output.GetAwaiter().GetResult()
Write-Output "RESTORED_DATABASE=$restoreDatabase"
'SELECT count(*) AS users FROM accounts.users; SELECT count(*) AS identities FROM accounts.external_identities; SELECT count(*) AS orphans FROM accounts.users u WHERE NOT EXISTS (SELECT 1 FROM accounts.external_identities i WHERE i.user_id=u.user_id);' |
    docker exec -i server-db sh -c "psql -X -v ON_ERROR_STOP=1 -U `"`$POSTGRES_USER`" -d $restoreDatabase"
if ($LASTEXITCODE -ne 0) { throw 'Restore verification failed.' }
Write-Output 'New restore-check DB retained for inspection. No original DB or container restarted.'
