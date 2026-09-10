#!/bin/sh
set -eu
cd /workspace/Server
dotnet tool install dotnet-ef --version 9.0.19 --tool-path /tmp/game-ef
export PATH="$PATH:/tmp/game-ef"
case "${1:-}" in
  generate)
    dotnet ef migrations add InitialAccounts --project DotsAndBoxes.Server --output-dir Accounts/Migrations
    dotnet ef migrations script --idempotent --project DotsAndBoxes.Server --output Tools/InitialAccounts.sql
    ;;
  migrate)
    dotnet ef database update --project DotsAndBoxes.Server
    dotnet ef database update --project DotsAndBoxes.Server
    dotnet ef migrations has-pending-model-changes --project DotsAndBoxes.Server
    ;;
  test)
    dotnet test DotsAndBoxes.Server.Tests --logger 'console;verbosity=normal'
    ;;
  *) echo 'Expected generate, migrate, or test' >&2; exit 2 ;;
esac
