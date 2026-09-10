param(
    [string]$ClientPath = "",
    [string]$ServerUrl = "http://localhost:5049",
    [switch]$SimulatePlayerOneResponseLossOnce
)

$ErrorActionPreference = "Stop"

if ( [string]::IsNullOrWhiteSpace($ClientPath) )
{
    $ClientPath = Join-Path $PSScriptRoot "..\..\Builds\Dots&Boxes.exe"
}

$resolvedClientPath = (Resolve-Path -LiteralPath $ClientPath).Path
$normalizedServerUrl = $ServerUrl.TrimEnd("/")
$serverProcess = $null
$logDirectory = Join-Path $PSScriptRoot "..\..\Logs\LocalUnityMatch"

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$playerOneLogPath = Join-Path $logDirectory "PlayerOne.log"
$playerTwoLogPath = Join-Path $logDirectory "PlayerTwo.log"

try
{
    Invoke-RestMethod -Method Get -Uri "$normalizedServerUrl/health/live" | Out-Null
}
catch
{
    $serverProject = Join-Path $PSScriptRoot "..\DotsAndBoxes.Server\DotsAndBoxes.Server.csproj"
    $serverArguments = @(
        "run" ,
        "--project" ,
        "`"$serverProject`"" ,
        "--urls" ,
        $normalizedServerUrl
    )

    $serverStartParameters = @{
        FilePath = "dotnet"
        ArgumentList = $serverArguments
        WindowStyle = "Hidden"
        PassThru = $true
    }

    $serverProcess = Start-Process @serverStartParameters

    $serverStarted = $false

    for ( $attempt = 0; $attempt -lt 60; $attempt++ )
    {
        Start-Sleep -Milliseconds 500

        if ( $serverProcess.HasExited )
        {
            throw "로컬 Game Server가 시작 중 종료됐습니다. ExitCode: $($serverProcess.ExitCode)"
        }

        try
        {
            Invoke-RestMethod -Method Get -Uri "$normalizedServerUrl/health/live" | Out-Null
            $serverStarted = $true
            break
        }
        catch
        {
        }
    }

    if ( !$serverStarted )
    {
        throw "로컬 Game Server가 30초 안에 시작되지 않았습니다."
    }
}

$match = Invoke-RestMethod -Method Post -Uri "$normalizedServerUrl/development/matches"

$playerOneArguments = @(
    "--server-url=$normalizedServerUrl" ,
    "--match-id=$($match.matchId)" ,
    "--user-id=$($match.playerOneUserId)" ,
    "-logFile" ,
    $playerOneLogPath
)

$playerTwoArguments = @(
    "--server-url=$normalizedServerUrl" ,
    "--match-id=$($match.matchId)" ,
    "--user-id=$($match.playerTwoUserId)" ,
    "-logFile" ,
    $playerTwoLogPath
)

if ( $SimulatePlayerOneResponseLossOnce )
{
    $playerOneArguments += "--simulate-confirm-response-loss-once"
}

$playerOneStartParameters = @{
    FilePath = $resolvedClientPath
    ArgumentList = $playerOneArguments
    PassThru = $true
}

$playerTwoStartParameters = @{
    FilePath = $resolvedClientPath
    ArgumentList = $playerTwoArguments
    PassThru = $true
}

$playerOneProcess = Start-Process @playerOneStartParameters
$playerTwoProcess = Start-Process @playerTwoStartParameters

Write-Output "Unity 로컬 온라인 Match를 시작했습니다."
Write-Output "MatchId: $($match.matchId)"
Write-Output "Player 1 ProcessId: $($playerOneProcess.Id)"
Write-Output "Player 2 ProcessId: $($playerTwoProcess.Id)"
Write-Output "Player 1 Log: $playerOneLogPath"
Write-Output "Player 2 Log: $playerTwoLogPath"

if ( $serverProcess -ne $null )
{
    Write-Output "Server ProcessId: $($serverProcess.Id)"
}
