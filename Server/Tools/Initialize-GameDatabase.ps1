param(
    [ValidateSet('Provision', 'Grant')][string]$Phase = 'Provision',
    [string]$SecretDirectory = 'C:\Users\HM_MiniPC\server\.secrets\dotsandboxes'
)
$ErrorActionPreference = 'Stop'

function Invoke-AdminSql([string]$Database, [string]$Sql) {
    # Secrets go over stdin, never command-line arguments or console output.
    $result = $Sql | docker exec -i server-db sh -c "psql -X -q -t -A -v ON_ERROR_STOP=1 -U `"`$POSTGRES_USER`" -d $Database" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Database administration failed for $Database. Output suppressed to protect credentials; do not rerun Provision blindly." }
    return ($result -join "`n").Trim()
}

$targets = @(
    @{ Database='dotsandboxes'; App='dotsandboxes_app'; Migrator='dotsandboxes_migrator' },
    @{ Database='dotsandboxes_test'; App='dotsandboxes_test_app'; Migrator='dotsandboxes_test_migrator' }
)

if ($Phase -eq 'Provision') {
    $collision = Invoke-AdminSql postgres @'
SELECT (SELECT count(*) FROM pg_database WHERE datname IN ('dotsandboxes','dotsandboxes_test'))
 + (SELECT count(*) FROM pg_roles WHERE rolname IN
 ('dotsandboxes_app','dotsandboxes_migrator','dotsandboxes_test_app','dotsandboxes_test_migrator'));
'@
    if ($collision -ne '0') { throw 'A requested DB or role already exists; no changes made.' }
    if (Test-Path -LiteralPath $SecretDirectory) { throw 'Secret directory already exists; no changes made.' }
    $null = New-Item -ItemType Directory -Path $SecretDirectory
    $acl = [System.Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    foreach ($sid in @($identity, [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [System.Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $SecretDirectory -AclObject $acl

    foreach ($target in $targets) {
        $appPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        $migrationPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        $database = $target.Database; $app = $target.App; $migrator = $target.Migrator
        $runtimeConnection = "Host=server-db;Port=5432;Database=$database;Username=$app;Password=$appPassword;Include Error Detail=false"
        $migrationConnection = "Host=server-db;Port=5432;Database=$database;Username=$migrator;Password=$migrationPassword;Include Error Detail=false"
        # Persist generated credentials before DB mutation, so partial failure is recoverable.
        if ($database -eq 'dotsandboxes') {
            [IO.File]::WriteAllText((Join-Path $SecretDirectory 'runtime.env'), "ConnectionStrings__GameDatabase=$runtimeConnection`n")
            [IO.File]::WriteAllText((Join-Path $SecretDirectory 'migration.env'), "GameDatabaseMigration=$migrationConnection`n")
        } else {
            [IO.File]::WriteAllText((Join-Path $SecretDirectory 'test.env'), "GameDatabaseMigration=$migrationConnection`nGAME_DATABASE_TEST_MIGRATION=$migrationConnection`nGAME_DATABASE_TEST_RUNTIME=$runtimeConnection`n")
        }
        $null = Invoke-AdminSql postgres @"
SET log_statement = 'none';
SET log_min_error_statement = 'panic';
BEGIN;
CREATE ROLE $migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '$migrationPassword';
CREATE ROLE $app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '$appPassword';
COMMIT;
CREATE DATABASE $database OWNER $migrator TEMPLATE template0 ENCODING 'UTF8';
REVOKE ALL ON DATABASE $database FROM PUBLIC;
GRANT CONNECT ON DATABASE $database TO $app;
"@
        $null = Invoke-AdminSql $database @"
REVOKE ALL ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA accounts AUTHORIZATION $migrator;
GRANT USAGE ON SCHEMA accounts TO $app;
"@
        Write-Output "Created $database; runtime=$app; migration=$migrator. Secrets saved with restricted NTFS ACL."
    }
} else {
    foreach ($target in $targets) {
        $null = Invoke-AdminSql $target.Database "GRANT SELECT, INSERT, UPDATE, DELETE ON accounts.users, accounts.external_identities TO $($target.App);"
        Write-Output "Granted account-table DML to $($target.App); no DDL, migration history or sequence permissions granted."
    }
}
