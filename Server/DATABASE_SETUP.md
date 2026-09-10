# Dots & Boxes 계정 DB 구축 결과 / 인계

작성일: 2026-09-07 (Asia/Seoul)

## 완료 상태

- 기존 PostgreSQL 인스턴스에 `dotsandboxes`와 `dotsandboxes_test` DB 생성 완료.
- 두 DB의 `accounts.users`, `accounts.external_identities` 초기 마이그레이션 적용 완료.
- 런타임/마이그레이션 역할 분리 및 실제 권한 검증 완료.
- EF Core 계정 저장 계층 구현 및 실제 PostgreSQL 통합 테스트 포함 **76/76 통과**.
- 독립 프로세스 재실행, 강제 UNIQUE 충돌, 트랜잭션 롤백, 백업 복원 검증 완료.
- 새 이미지 `dotsandboxes-server:accounts-20260907` 빌드 및 임시 컨테이너 검증 완료.
- **현재 공개 게임 컨테이너에는 아직 새 이미지와 연결 설정을 반영하지 않았다.**
  기존 게임 서비스 재생성은 접속 중단이 생길 수 있어 사용자 승인 후 별도 적용한다.
- Google 서버 인증, JWT 발급, SignalR 인증 교체, Unity 연결은 이번 구현/검증 범위가 아니다.

## 환경과 저장 위치

| 항목 | 값 |
|---|---|
| OS | Windows 11 Pro 64-bit, 10.0.22631 |
| 저장소 | `C:\Users\HM_MiniPC\server\Dots-Boxes` |
| 작업 브랜치 | `Feature/GoogleLogin` |
| 기준 HEAD | `c69f912e0bb8878a2e15770e3c24be3ca46cf732` |
| 커밋 여부 | 커밋하지 않음; 작업 트리 변경으로 인계 |
| PostgreSQL | 기존 `server-db`, `postgres:15-alpine`, 실제 버전 15.18 |
| 내부 주소 | `server-db:5432`, Network `server_server-network` |
| DB Host 포트 | 미공개, 기존 설정 유지 |
| 영속 저장 | `C:\Users\HM_MiniPC\server\postgres\data` → `/var/lib/postgresql/data` |
| 기존 DB | `admin`, `postgres` 유지 |
| .NET | 서버 `net9.0`, Shared `netstandard2.1` 유지 |
| 빌드 SDK | Docker `mcr.microsoft.com/dotnet/sdk:9.0`, 실행 시 9.0.317 |
| 런타임 이미지 | `mcr.microsoft.com/dotnet/aspnet:9.0`, 기본 사용자 `app` |

Host에 SDK나 PostgreSQL을 추가 설치하지 않았다. 기존 8개 컨테이너의 ID, StartedAt,
RestartCount가 작업 전후 동일하며 기존 게임 Health도 정상이다. Nginx/DNS/기존 Compose 파일은 수정하지 않았다.

## DB와 권한

| DB | 소유자/마이그레이션 역할 | 런타임 역할 |
|---|---|---|
| `dotsandboxes` | `dotsandboxes_migrator` | `dotsandboxes_app` |
| `dotsandboxes_test` | `dotsandboxes_test_migrator` | `dotsandboxes_test_app` |

- 네 역할 모두 LOGIN 가능, NOSUPERUSER/NOCREATEDB/NOCREATEROLE/NOREPLICATION.
- 새 DB에만 PUBLIC 권한을 회수하고 런타임에 CONNECT와 `accounts` 스키마 USAGE 부여.
- 런타임은 두 계정 테이블의 SELECT/INSERT/UPDATE/DELETE만 보유.
- 런타임의 스키마/테이블 생성, ALTER TABLE, 마이그레이션 이력 조회는 거부됨을 테스트했다.
- UUID를 앱에서 생성하므로 시퀀스 권한은 부여하지 않았다.
- 마이그레이션 이력은 `accounts."__EFMigrationsHistory"`에 저장한다.
- 기존 DB, 역할, 스키마의 권한은 변경하지 않았다. 역할은 PostgreSQL 인스턴스 전체 이름 공간에 생성된다.
- 운영 계정 DB에는 테스트 데이터를 넣지 않았다. 읽기 검증 시 Users=0 / ExternalIdentities=0이었다.

## 모델과 패키지

스키마는 `accounts`, 물리 이름은 snake_case이다.

- `users`: `user_id uuid` PK, NOT NULL 표시 이름/UTC 생성·수정 시각.
- `external_identities`: `(provider, provider_application_id, provider_player_id)` 복합 PK,
  `user_id` FK 및 역조회 인덱스. 삭제는 RESTRICT.
- Guid.Empty와 Unicode 공백만 있는 문자열을 DB CHECK와 서비스 검증으로 거부.
- 표시 이름은 UNIQUE가 아니다. 식별자는 trim/case-fold/숫자 변환 없이 원문 저장.
- 토큰/인증코드/Client Secret/JWT/경기 상태를 저장하는 컬럼은 없다.

| 패키지/도구 | 버전 |
|---|---|
| Microsoft.EntityFrameworkCore | 9.0.19 |
| Microsoft.EntityFrameworkCore.Relational | 9.0.19 |
| Microsoft.EntityFrameworkCore.Design | 9.0.19, PrivateAssets=all |
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.4 |
| dotnet-ef | 9.0.19, 임시 SDK 컨테이너에만 설치 |

Relational 패키지를 명시해 프로젝트 참조에서 서로 다른 패치 버전이 섞이는 MSB3277 경고를 해결했다.
최종 전체 테스트는 `-warnaserror`로 통과했다.

PostgreSQL 15는 지원 중이나 확인 당시 최신 15.x 패치보다 낮다. 패치 업데이트는 별도 유지보수 대상으로 남겼다.
EF Core 9 지원 종료 예정일은 2026-11-10이므로 이후 프레임워크/패키지 전환을 별도 계획해야 한다.
이번에는 .NET 10으로 변경하지 않았다.

## 계정 서비스 계약 / 다음 인증 개발자 인계

네임스페이스: `DotsAndBoxes.Server.Accounts`

```csharp
public interface IExternalAccountService
{
    Task<ExternalUserResult> GetOrCreateExternalUser_async(
        string provider,
        string providerApplicationId,
        string providerPlayerId,
        string? verifiedDisplayName,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalUserResult(Guid UserId, string DisplayName);
```

DI: `builder.Services.AddGameAccounts(builder.Configuration);` — `Program.cs`에 등록했다.
`IDbContextFactory<GameDbContext>`를 사용하며 요청마다 독립 DbContext를 생성한다.

호출 지점: 후속 로그인 API에서 Google 인증 코드의 서버 검증이 성공한 뒤, JWT를 발급하기 전에 호출한다.
provider는 `ExternalAccountService.GOOGLE_PLAY_GAMES`, application ID는 **서버에 설정한 PGS 게임 프로젝트 ID**,
player ID와 표시 이름은 Google 검증 결과를 전달한다. Android 패키지명/Web Client ID를 application ID로 사용하지 않는다.
UserId는 기존 경기 코드와 동일한 `Guid`이다.

이 서비스는 인증기가 아니다. 임의 Player ID를 받는 공개 로그인 API를 새로 만들지 않았고,
닉네임/이메일로 계정을 병합하거나 외부 연결의 UserId를 옮기지 않는다.

동작:

- 신규 계정은 users와 external_identities를 한 트랜잭션으로 저장.
- 복합 PK 충돌 시 실패 트랜잭션 전체를 롤백하고 추적 상태를 비운 후, 트랜잭션을 dispose하고 승자 계정을 재조회.
- 기존 계정은 UserId/CreatedAtUtc를 유지하고 유효한 새 닉네임만 갱신.
- 빈 닉네임/프로필 조회 실패 시: 검증된 player ID가 있다는 전제에서 null을 전달한다.
  기존 닉네임은 보존하고 신규 계정만 기본 이름 `플레이어`를 사용한다.
- 여러 정상 닉네임 업데이트가 겹치면 마지막으로 저장된 값이 남는다.
- 앱 시작에서 Migrate/EnsureCreated/EnsureDeleted를 실행하지 않는다.
- 연결 설정은 계정 계층이 처음 resolve될 때 검증한다. 기존 게임 전용 시작/테스트는 DB 설정 없이 계속 가능하다.
  기존 `/health/ready`는 DB readiness를 의미하지 않는다. DB 검증은 별도 AccountProbe로 수행했다.

## 비밀 설정

Host 경로 (저장소 바깥):

```text
C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\runtime.env
C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\migration.env
C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\test.env
```

- `runtime.env`: `ConnectionStrings__GameDatabase` (앱 설정 키 `ConnectionStrings:GameDatabase`).
- `migration.env`: `GameDatabaseMigration` (마이그레이션 전용; 런타임에 전달하지 않음).
- `test.env`: `GameDatabaseMigration`, `GAME_DATABASE_TEST_MIGRATION`, `GAME_DATABASE_TEST_RUNTIME`.
- 비밀번호는 32바이트 난수의 hex 표현으로 생성했다. 실제 값은 소스/문서/채팅에 기록하지 않는다.
- NTFS 상속을 끊고 `HM_MiniPC`, `SYSTEM`, `Administrators`에만 권한 부여.
- Docker 실행 시 `--env-file`로 전달. Docker 관리자 권한 보유자는 컨테이너 환경을 볼 수 있으므로
  raw `docker inspect`/`docker compose config` 전체 출력을 공유하지 않는다. 구성 검사는 `config --quiet`를 사용한다.
- 새 이미지의 기본 `app` 사용자에서 실제 런타임 자격증명으로 DI 생성과 두 테이블 읽기가 성공했다.

다른 개발자의 로컬 설정은 자기 로컬 PostgreSQL과 별도 비밀번호로 구성한다.
예를 들어 설치된 로컬 SDK가 있는 개발 PC에서는 User Secrets를 사용한다 (아래 비밀번호는 플레이스홀더).

```powershell
dotnet user-secrets init --project Server/DotsAndBoxes.Server
dotnet user-secrets set "ConnectionStrings:GameDatabase" "Host=localhost;Database=dotsandboxes_local;Username=dotsandboxes_local_app;Password=<LOCAL_PASSWORD>" --project Server/DotsAndBoxes.Server
```

로컬 마이그레이션에는 별도 로컬 관리 역할의 연결 문자열을 `GameDatabaseMigration`으로 주입한다.
Unity에는 DB 연결 문자열이나 DB 비밀번호를 전달하지 않는다. N100 DB 포트를 외부 공개하지 않는다.

## 마이그레이션과 재현 명령

마이그레이션 이름: `20260907045416_InitialAccounts`

- 코드/스냅샷: `Server/DotsAndBoxes.Server/Accounts/Migrations/`
- 검토용 재적용 가능 SQL: `Server/Tools/InitialAccounts.sql`
- 초기 역할 생성 스크립트: `Server/Tools/Initialize-GameDatabase.ps1`
  이미 DB/역할/비밀 디렉터리가 있으면 중단한다. **현재 Host에서 Provision을 다시 실행하지 않는다.**
- 테이블 권한 부여만 재실행: `Initialize-GameDatabase.ps1 -Phase Grant`.

N100에서 이미 적용한 마이그레이션 상태를 다시 확인/적용하는 명령:

```powershell
docker run --rm --name dotsandboxes-db-migrate --network server_server-network `
  --env-file C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\migration.env `
  --mount type=bind,source=C:\Users\HM_MiniPC\server\Dots-Boxes,target=/workspace `
  --workdir /workspace/Server mcr.microsoft.com/dotnet/sdk:9.0 `
  sh /workspace/Server/Tools/database-tools.sh migrate
```

테스트 DB는 위 명령의 env-file을 `test.env`로 바꾸면 된다. `migrate`는 두 번 적용 후 모델 차이 유무도 검사한다.
운영/테스트 DB 둘 다 재적용에서 "already up to date", 모델 검사에서 "No changes"가 확인됐다.
첫 빈 DB 적용 중 아직 없는 이력 테이블 SELECT에 `Failed executing DbCommand`가 한 번 출력됐다.
이후 이력 테이블/스키마 생성 및 적용이 성공했고 최종 exit code는 0이었다. 다음 실행에는 그 오류가 없다.

전체 테스트:

```powershell
docker run --rm --name dotsandboxes-db-integration --network server_server-network `
  --env-file C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\test.env `
  --mount type=bind,source=C:\Users\HM_MiniPC\server\Dots-Boxes,target=/workspace `
  --workdir /workspace/Server mcr.microsoft.com/dotnet/sdk:9.0 `
  dotnet test DotsAndBoxes.Server.Tests -warnaserror --logger "console;verbosity=normal"
```

テ스트 환경 변수가 없으면 DB 테스트만 명시적으로 skip한다. 실제 DB 테스트는 DB명이
`dotsandboxes_test`인지 검사하며 다른 DB 자격증명으로 실행을 거부한다.
테스트는 매 실행마다 독립 application ID를 사용한다. 합성 테스트 데이터는 테스트 DB에 남는다.

## 검증 결과

최종 76개 테스트 전체 통과 (기존 65개 + 계정 DB 11개), 빌드 경고를 오류로 처리한 실행에서 성공.

| 요구 사항 | 결과 |
|---|---|
| 신규/재적용 마이그레이션 | 운영/테스트 DB 모두 성공 |
| 최초 가입 users 1개 / identity 1개 | 통과 |
| 같은 식별자 재조회 | 동일 UserId, 중복 없음 |
| 8개 동시 요청 | 7개 PK 충돌을 실제 유도; 모두 동일 UserId, 고아 0 |
| 닉네임 중복, Player ID 대소문자, application ID 분리 | 서로 다른 UserId |
| 닉네임 갱신 | UserId/CreatedAtUtc 유지 |
| 빈 식별자, Unicode 공백, Guid.Empty, FK 위반 | 거부 확인 |
| 두 INSERT 후 commit 전 취소 주입 | 사용자/외부 연결 모두 롤백 |
| 런타임 계정 DML/DDL 권한 | 계정 서비스 동작 / DDL 거부 |
| 독립 프로세스 재실행 | `existing=False` 다음 `existing=True`, 같은 UserId |
| 실제 런타임 env-file + app 사용자 | DI/테이블 읽기 성공, 운영 계정 데이터 0건 유지 |
| 새 서버 Docker 이미지 | Release publish 성공, 내부 Health 3종 HTTP 200, Warning/Error 없음 |
| 백업 복원 | 사용자/연결 각 21개, 고아 0, 영속성 테스트 UserId 일치 |
| 기존 서비스 | 8개 컨테이너 ID/StartedAt/RestartCount 동일 |

공유 DB 인스턴스는 재시작하지 않았다. 데이터 영속 경로 확인, 독립 클라이언트 프로세스 재실행 및
별도 DB 백업 복원으로 대체 검증했다. Google 실계정 인증은 검증하지 않았다.

## 백업 및 복원

백업 (custom format, 비밀 디렉터리 아래 NTFS 권한 상속):

```powershell
& C:\Users\HM_MiniPC\server\Dots-Boxes\Server\Tools\Backup-GameDatabase.ps1 -Database dotsandboxes
```

생성한 파일:

```text
.secrets\dotsandboxes\backups\dotsandboxes-20260907-140206.dump
.secrets\dotsandboxes\backups\dotsandboxes_test-20260907-140238.dump
```

새 DB로만 복원 검증 (원본 덮어쓰기, --clean, DB drop 없음):

```powershell
& C:\Users\HM_MiniPC\server\Dots-Boxes\Server\Tools\Test-GameDatabaseRestore.ps1 `
  -BackupPath C:\Users\HM_MiniPC\server\.secrets\dotsandboxes\backups\dotsandboxes_test-20260907-140238.dump
```

실제 복원 DB `dotsandboxes_restorecheck_20260907140313`은 확인용으로 유지했다.
원본 테스트 DB와 사용자 수 및 영속성 테스트 UserId가 일치한다.
이 스크립트는 복원검증 DB를 테스트 migrator 소유로 생성하고 `--no-owner --no-privileges`로 복원한다.
실제 장애 복구는 새 DB의 테이블/제약 검증 후 별도 runtime 권한을 부여하고 연결 대상을 전환해야 한다.
DB dump에는 역할 비밀번호가 포함되지 않으므로 자격증명 파일은 별도로 안전하게 백업한다.
정기/외부 백업 자동화와 운영 장애 복구 전환은 이번에 설정하지 않았다.

## 공개 게임 서버 반영: 승인 후에만

추가한 `Server/compose.accounts.yaml`은 기본 Compose에 자동 적용되지 않는 명시적 override다.
실제 기존 Compose 파일은 수정하지 않았고, 위 override의 `config --quiet` 검증만 수행했다.
기존 `version` 속성이 obsolete라는 경고는 이전부터 존재한다.

반영하면 `dotsandboxes-server` 한 컨테이너가 재생성되어 진행 중 연결/메모리 경기가 끊길 수 있다.
Google 인증 API가 아직 없으므로 이 반영만으로 Google 로그인이 완성되지는 않는다.

```powershell
# 사용자에게 적용 시점 승인을 받은 뒤 실행
docker compose -f C:\Users\HM_MiniPC\server\docker-compose.yml `
  -f C:\Users\HM_MiniPC\server\Dots-Boxes\Server\compose.accounts.yaml `
  up -d --no-deps --no-build dotsandboxes-server
```

반영 후에도 Host 포트/Network/Nginx/DNS와 Development 환경은 기존 설정을 따른다.
향후 관리 명령에도 두 Compose 파일을 함께 지정해야 한다.

반영 후 앱만 원복해야 하면 (DB/계정 데이터는 보존):

```powershell
docker compose -f C:\Users\HM_MiniPC\server\docker-compose.yml `
  up -d --no-deps --no-build --force-recreate dotsandboxes-server
```

이 명령은 현재 기본 Compose의 기존 이미지 `dotsandboxes-server:0.1.0`을 사용한다.
DB Down migration, DB drop, compose down은 롤백에 사용하지 않는다.

## 변경 파일 목록

- `Server/DotsAndBoxes.Server/DotsAndBoxes.Server.csproj`
- `Server/DotsAndBoxes.Server/Program.cs`
- `Server/DotsAndBoxes.Server/Accounts/GameUser.cs`
- `Server/DotsAndBoxes.Server/Accounts/ExternalIdentity.cs`
- `Server/DotsAndBoxes.Server/Accounts/GameDbContext.cs`
- `Server/DotsAndBoxes.Server/Accounts/ExternalAccountService.cs`
- `Server/DotsAndBoxes.Server/Accounts/AccountServiceCollectionExtensions.cs`
- `Server/DotsAndBoxes.Server/Accounts/GameDbContextDesignFactory.cs`
- `Server/DotsAndBoxes.Server/Accounts/Migrations/20260907045416_InitialAccounts.cs`
- `Server/DotsAndBoxes.Server/Accounts/Migrations/20260907045416_InitialAccounts.Designer.cs`
- `Server/DotsAndBoxes.Server/Accounts/Migrations/GameDbContextModelSnapshot.cs`
- `Server/DotsAndBoxes.Server.Tests/Accounts/AccountDatabaseTests.cs`
- `Server/Tools/Initialize-GameDatabase.ps1`
- `Server/Tools/InitialAccounts.sql`
- `Server/Tools/database-tools.sh`
- `Server/Tools/AccountProbe/AccountProbe.csproj`
- `Server/Tools/AccountProbe/Program.cs`
- `Server/Tools/Backup-GameDatabase.ps1`
- `Server/Tools/Test-GameDatabaseRestore.ps1`
- `Server/compose.accounts.yaml`
- `Server/DATABASE_SETUP.md`

저장소 밖 변경: `.secrets\dotsandboxes\`의 env-file 3개와 백업 2개.
새 DB/역할/검증 이미지 외 기존 서비스 리소스는 그대로 유지했다.
빌드/테스트용 임시 컨테이너와 임시 앱 검증 컨테이너는 완료 후 제거했다.

참고: [EF Core 호환성](https://learn.microsoft.com/en-us/ef/core/miscellaneous/platforms),
[EF Core 지원 일정](https://learn.microsoft.com/en-us/ef/core/what-is-new/),
[PostgreSQL 지원 일정](https://www.postgresql.org/support/versioning/).
