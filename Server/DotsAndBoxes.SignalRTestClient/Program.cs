using System.Net.Http.Json;
using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR.Client;

const string SERVER_URL = "http://localhost:5049";

using HttpClient httpClient = new HttpClient
{
    BaseAddress = new Uri(SERVER_URL)
};

using HttpResponseMessage createMatchResponse =
    await httpClient.PostAsync("/development/matches", null);

createMatchResponse.EnsureSuccessStatusCode();

DevelopmentMatchResponse? developmentMatch =
    await createMatchResponse.Content
        .ReadFromJsonAsync<DevelopmentMatchResponse>();

if ( developmentMatch == null )
{
    throw new InvalidOperationException(
        "개발용 매치 생성 응답을 읽지 못했습니다.");
}

await using HubConnection playerOneConnection =
    CreateConnection(
        developmentMatch.HubPath,
        developmentMatch.PlayerOneUserId);

await using HubConnection playerTwoConnection =
    CreateConnection(
        developmentMatch.HubPath,
        developmentMatch.PlayerTwoUserId);

TaskCompletionSource<MatchSnapshot> playerOneBroadcast =
    new TaskCompletionSource<MatchSnapshot>(
        TaskCreationOptions.RunContinuationsAsynchronously);

TaskCompletionSource<MatchSnapshot> playerTwoBroadcast =
    new TaskCompletionSource<MatchSnapshot>(
        TaskCreationOptions.RunContinuationsAsynchronously);

playerOneConnection.On<MatchSnapshot>(
    "MatchStateChanged" ,
    snapshot =>
    {
        playerOneBroadcast.TrySetResult(snapshot);
    });

playerTwoConnection.On<MatchSnapshot>(
    "MatchStateChanged" ,
    snapshot =>
    {
        playerTwoBroadcast.TrySetResult(snapshot);
    });

await playerOneConnection.StartAsync();
await playerTwoConnection.StartAsync();

MatchSnapshot playerOneInitialSnapshot =
    await playerOneConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch",
        developmentMatch.MatchId);

MatchSnapshot playerTwoInitialSnapshot =
    await playerTwoConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch",
        developmentMatch.MatchId);

Ensure(
    playerOneInitialSnapshot.Revision == 0 ,
    "Player 1 초기 Revision이 0이 아닙니다.");

Ensure(
    playerTwoInitialSnapshot.Revision == 0 ,
    "Player 2 초기 Revision이 0이 아닙니다.");

ConfirmEdgeRequest request = new ConfirmEdgeRequest
{
    MatchId = developmentMatch.MatchId,
    EdgeId = 0,
    ExpectedRevision = playerOneInitialSnapshot.Revision,
    RequestId = Guid.NewGuid()
};

ConfirmEdgeResponse response =
    await playerOneConnection.InvokeAsync<ConfirmEdgeResponse>(
        "ConfirmEdge",
        request);

Ensure(
    response.IsAccepted ,
    $"ConfirmEdge가 거부됐습니다. Error: {response.Error}");

MatchSnapshot playerOneEventSnapshot =
    await playerOneBroadcast.Task.WaitAsync(
        TimeSpan.FromSeconds(5));

MatchSnapshot playerTwoEventSnapshot =
    await playerTwoBroadcast.Task.WaitAsync(
        TimeSpan.FromSeconds(5));

MatchSnapshot playerTwoSyncSnapshot =
    await playerTwoConnection.InvokeAsync<MatchSnapshot>(
        "RequestSync",
        developmentMatch.MatchId);

Ensure(
    playerOneEventSnapshot.Revision == 1 ,
    "Player 1 Broadcast Revision이 1이 아닙니다.");

Ensure(
    playerTwoEventSnapshot.Revision == 1 ,
    "Player 2 Broadcast Revision이 1이 아닙니다.");

Ensure(
    playerTwoSyncSnapshot.Revision == 1 ,
    "RequestSync Revision이 1이 아닙니다.");

Ensure(
    playerOneEventSnapshot.EdgeOwners[ 0 ] ==
        PLAYER_INDEX_ENUM.PLAYER_ONE ,
    "Player 1 Broadcast에 Edge 상태가 반영되지 않았습니다.");

Ensure(
    playerTwoEventSnapshot.EdgeOwners[ 0 ] ==
        PLAYER_INDEX_ENUM.PLAYER_ONE ,
    "Player 2 Broadcast에 Edge 상태가 반영되지 않았습니다.");

Ensure(
    playerTwoSyncSnapshot.EdgeOwners[ 0 ] ==
        PLAYER_INDEX_ENUM.PLAYER_ONE ,
    "RequestSync 결과에 Edge 상태가 반영되지 않았습니다.");

ConfirmEdgeResponse retriedResponse =
    await playerOneConnection.InvokeAsync<ConfirmEdgeResponse>(
        "ConfirmEdge",
        request);

Ensure(
    retriedResponse.IsAccepted ,
    $"동일 RequestId 재전송이 거부됐습니다. Error: {retriedResponse.Error}");

Ensure(
    retriedResponse.RequestId == request.RequestId ,
    "재전송 응답의 RequestId가 일치하지 않습니다.");

Ensure(
    retriedResponse.Snapshot != null &&
    retriedResponse.Snapshot.Revision == 1 ,
    "동일 요청 재전송 후 Revision이 변경됐습니다.");

MatchSnapshot afterRetrySnapshot =
    await playerTwoConnection.InvokeAsync<MatchSnapshot>(
        "RequestSync",
        developmentMatch.MatchId);

int confirmedEdgeCount =
    afterRetrySnapshot.EdgeOwners.Count(
        owner => owner != PLAYER_INDEX_ENUM.NONE);

Ensure(
    afterRetrySnapshot.Revision == 1 ,
    "동일 요청 재전송으로 Revision이 증가했습니다.");

Ensure(
    confirmedEdgeCount == 1 ,
    "동일 요청 재전송으로 Edge가 추가 확정됐습니다.");

ConfirmEdgeRequest conflictingRequest =
    new ConfirmEdgeRequest
    {
        MatchId = developmentMatch.MatchId,
        EdgeId = 1,
        ExpectedRevision = 0,
        RequestId = request.RequestId
    };

ConfirmEdgeResponse conflictingResponse =
    await playerOneConnection.InvokeAsync<ConfirmEdgeResponse>(
        "ConfirmEdge",
        conflictingRequest);

Ensure(
    !conflictingResponse.IsAccepted ,
    "변경된 내용으로 재사용한 RequestId가 승인됐습니다.");

Ensure(
    conflictingResponse.Error ==
        MATCH_COMMAND_ERROR_ENUM.DUPLICATE_REQUEST_CONFLICT ,
    $"예상하지 않은 충돌 오류입니다: {conflictingResponse.Error}");

Ensure(
    conflictingResponse.Snapshot != null &&
    conflictingResponse.Snapshot.EdgeOwners[ 1 ] ==
        PLAYER_INDEX_ENUM.NONE ,
    "충돌 요청이 Edge 상태를 변경했습니다.");

ConfirmEdgeRequest staleRevisionRequest =
    new ConfirmEdgeRequest
    {
        MatchId = developmentMatch.MatchId,
        EdgeId = 1,
        ExpectedRevision = 0,
        RequestId = Guid.NewGuid()
    };

ConfirmEdgeResponse staleRevisionResponse =
    await playerTwoConnection.InvokeAsync<ConfirmEdgeResponse>(
        "ConfirmEdge",
        staleRevisionRequest);

Ensure(
    !staleRevisionResponse.IsAccepted ,
    "오래된 Revision 요청이 승인됐습니다.");

Ensure(
    staleRevisionResponse.Error ==
        MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH ,
    $"예상하지 않은 Revision 오류입니다: {staleRevisionResponse.Error}");

Ensure(
    staleRevisionResponse.ShouldRequestSync ,
    "Revision 불일치인데 Sync 요청 표시가 없습니다.");

MatchSnapshot staleRevisionSnapshot =
    staleRevisionResponse.Snapshot ??
    throw new InvalidOperationException(
        "Revision 불일치 응답에 최신 Snapshot이 없습니다.");

Ensure(
    staleRevisionSnapshot.Revision == 1 ,
    "Revision 불일치 응답의 Snapshot Revision이 올바르지 않습니다.");

MatchSnapshot recoveredSnapshot =
    await playerTwoConnection.InvokeAsync<MatchSnapshot>(
        "RequestSync",
        developmentMatch.MatchId);

Ensure(
    recoveredSnapshot.Revision ==
        staleRevisionSnapshot.Revision ,
    "RequestSync 결과가 서버 최신 Revision과 일치하지 않습니다.");

playerTwoBroadcast =
    new TaskCompletionSource<MatchSnapshot>(
        TaskCreationOptions.RunContinuationsAsynchronously);

await playerOneConnection.StopAsync();

MatchSnapshot disconnectSnapshot =
    await playerTwoBroadcast.Task.WaitAsync(
        TimeSpan.FromSeconds(5));

Ensure(
    disconnectSnapshot.Revision == 2 ,
    "Disconnect 기권 후 Revision이 2가 아닙니다.");

Ensure(
    disconnectSnapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED ,
    "Disconnect 기권 후 Match가 종료되지 않았습니다.");

Ensure(
    disconnectSnapshot.GameResult == GAME_RESULT_ENUM.PLAYER_TWO_WIN ,
    "Player 1 Disconnect가 Player 2 승리로 처리되지 않았습니다.");

await RunFullMatch_async(httpClient , SERVER_URL);
await RunResponseLossRetry_async(httpClient , SERVER_URL);
await RunExplicitLeave_async(httpClient , SERVER_URL);
await RunMatchmaking_async(SERVER_URL);

Console.WriteLine("SignalR 통합 검증 성공");
Console.WriteLine($"MatchId: {developmentMatch.MatchId}");
Console.WriteLine("Revision: 0 → 1");
Console.WriteLine("Player 1 Broadcast 수신: 성공");
Console.WriteLine("Player 2 Broadcast 수신: 성공");
Console.WriteLine("Player 2 RequestSync 검증: 성공");
Console.WriteLine("동일 RequestId 재전송 검증: 성공");
Console.WriteLine("RequestId 충돌 거부 검증: 성공");
Console.WriteLine("Revision 불일치 복구 검증: 성공");
Console.WriteLine("Disconnect 기권 및 상대 승리 검증: 성공");
Console.WriteLine("두 Client 40개 Edge 전체 경기 및 최종 Snapshot 일치 검증: 성공");
Console.WriteLine("응답과 자기 Broadcast 유실 후 동일 RequestId 재전송 검증: 성공");
Console.WriteLine("명시적 LeaveMatch 기권 및 상대 결과 전달 검증: 성공");
Console.WriteLine("인메모리 일반전 매칭과 자동 MatchRoom 생성 검증: 성공");

static HubConnection CreateConnection(
    string hubPath ,
    Guid userId)
{
    string connectionUrl =
        $"{SERVER_URL}{hubPath}?userId={userId:D}";

    return new HubConnectionBuilder()
        .WithUrl(connectionUrl)
        .Build();
}

static void Ensure(bool condition , string errorMessage)
{
    if ( !condition )
    {
        throw new InvalidOperationException(errorMessage);
    }
}

static async Task RunFullMatch_async(HttpClient httpClient , string serverUrl)
{
    using HttpResponseMessage createMatchResponse =
        await httpClient.PostAsync("/development/matches" , null);

    createMatchResponse.EnsureSuccessStatusCode();

    DevelopmentMatchResponse developmentMatch =
        await createMatchResponse.Content
            .ReadFromJsonAsync<DevelopmentMatchResponse>() ??
        throw new InvalidOperationException(
            "전체 경기용 개발 Match 응답을 읽지 못했습니다.");

    await using HubConnection playerOneConnection =
        CreateFullMatchConnection(serverUrl , developmentMatch.HubPath , developmentMatch.PlayerOneUserId);

    await using HubConnection playerTwoConnection =
        CreateFullMatchConnection(serverUrl , developmentMatch.HubPath , developmentMatch.PlayerTwoUserId);

    TaskCompletionSource<MatchSnapshot> playerOneFinalBroadcast =
        CreateSnapshotCompletionSource();

    TaskCompletionSource<MatchSnapshot> playerTwoFinalBroadcast =
        CreateSnapshotCompletionSource();

    playerOneConnection.On<MatchSnapshot>("MatchStateChanged" , snapshot =>
    {
        if ( snapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED )
        {
            playerOneFinalBroadcast.TrySetResult(snapshot);
        }
    });

    playerTwoConnection.On<MatchSnapshot>("MatchStateChanged" , snapshot =>
    {
        if ( snapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED )
        {
            playerTwoFinalBroadcast.TrySetResult(snapshot);
        }
    });

    await playerOneConnection.StartAsync();
    await playerTwoConnection.StartAsync();

    MatchSnapshot playerOneInitialSnapshot =
        await playerOneConnection.InvokeAsync<MatchSnapshot>(
            "JoinMatch" ,
            developmentMatch.MatchId);

    MatchSnapshot playerTwoInitialSnapshot =
        await playerTwoConnection.InvokeAsync<MatchSnapshot>(
            "JoinMatch" ,
            developmentMatch.MatchId);

    EnsureSnapshotsEqual(
        playerOneInitialSnapshot ,
        playerTwoInitialSnapshot ,
        "두 Client의 전체 경기 초기 Snapshot");

    MatchSnapshot currentSnapshot = playerOneInitialSnapshot;

    for ( int edgeId = 0; edgeId < BoardTopology.EDGE_COUNT; edgeId++ )
    {
        HubConnection currentPlayerConnection =
            currentSnapshot.CurrentPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                ? playerOneConnection
                : playerTwoConnection;

        ConfirmEdgeRequest request = new ConfirmEdgeRequest
        {
            MatchId = developmentMatch.MatchId ,
            EdgeId = edgeId ,
            ExpectedRevision = currentSnapshot.Revision ,
            RequestId = Guid.NewGuid()
        };

        ConfirmEdgeResponse response =
            await currentPlayerConnection.InvokeAsync<ConfirmEdgeResponse>(
                "ConfirmEdge" ,
                request);

        Ensure(
            response.IsAccepted ,
            $"전체 경기 Edge {edgeId} Confirm이 거부됐습니다. Error: {response.Error}");

        currentSnapshot = response.Snapshot ??
            throw new InvalidOperationException(
                $"전체 경기 Edge {edgeId} 응답에 Snapshot이 없습니다.");

        Ensure(
            currentSnapshot.Revision == edgeId + 1 ,
            $"전체 경기 Edge {edgeId} 처리 후 Revision이 올바르지 않습니다.");
    }

    MatchSnapshot playerOneFinalSnapshot =
        await playerOneFinalBroadcast.Task.WaitAsync(TimeSpan.FromSeconds(5));

    MatchSnapshot playerTwoFinalSnapshot =
        await playerTwoFinalBroadcast.Task.WaitAsync(TimeSpan.FromSeconds(5));

    MatchSnapshot playerOneSyncSnapshot =
        await playerOneConnection.InvokeAsync<MatchSnapshot>(
            "RequestSync" ,
            developmentMatch.MatchId);

    MatchSnapshot playerTwoSyncSnapshot =
        await playerTwoConnection.InvokeAsync<MatchSnapshot>(
            "RequestSync" ,
            developmentMatch.MatchId);

    EnsureFinalSnapshot(currentSnapshot);
    EnsureSnapshotsEqual(currentSnapshot , playerOneFinalSnapshot , "Player 1 최종 Broadcast");
    EnsureSnapshotsEqual(currentSnapshot , playerTwoFinalSnapshot , "Player 2 최종 Broadcast");
    EnsureSnapshotsEqual(currentSnapshot , playerOneSyncSnapshot , "Player 1 최종 RequestSync");
    EnsureSnapshotsEqual(currentSnapshot , playerTwoSyncSnapshot , "Player 2 최종 RequestSync");
}

static async Task RunResponseLossRetry_async(HttpClient httpClient , string serverUrl)
{
    using HttpResponseMessage createMatchResponse =
        await httpClient.PostAsync("/development/matches" , null);

    createMatchResponse.EnsureSuccessStatusCode();

    DevelopmentMatchResponse developmentMatch =
        await createMatchResponse.Content
            .ReadFromJsonAsync<DevelopmentMatchResponse>() ??
        throw new InvalidOperationException(
            "응답 유실 검증용 개발 Match 응답을 읽지 못했습니다.");

    await using HubConnection playerOneConnection =
        CreateFullMatchConnection(
            serverUrl ,
            developmentMatch.HubPath ,
            developmentMatch.PlayerOneUserId ,
            true);

    await using HubConnection playerTwoConnection =
        CreateFullMatchConnection(
            serverUrl ,
            developmentMatch.HubPath ,
            developmentMatch.PlayerTwoUserId);

    TaskCompletionSource<MatchSnapshot> playerOneBroadcast =
        CreateSnapshotCompletionSource();

    TaskCompletionSource<MatchSnapshot> playerTwoBroadcast =
        CreateSnapshotCompletionSource();

    int playerOneBroadcastCount = 0;

    playerOneConnection.On<MatchSnapshot>("MatchStateChanged" , snapshot =>
    {
        Interlocked.Increment(ref playerOneBroadcastCount);
        playerOneBroadcast.TrySetResult(snapshot);
    });

    playerTwoConnection.On<MatchSnapshot>("MatchStateChanged" , snapshot =>
    {
        playerTwoBroadcast.TrySetResult(snapshot);
    });

    await playerOneConnection.StartAsync();
    await playerTwoConnection.StartAsync();

    MatchSnapshot initialSnapshot =
        await playerOneConnection.InvokeAsync<MatchSnapshot>(
            "JoinMatch" ,
            developmentMatch.MatchId);

    await playerTwoConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch" ,
        developmentMatch.MatchId);

    ConfirmEdgeRequest request = new ConfirmEdgeRequest
    {
        MatchId = developmentMatch.MatchId ,
        EdgeId = 0 ,
        ExpectedRevision = initialSnapshot.Revision ,
        RequestId = Guid.NewGuid()
    };

    ConfirmEdgeResponse ignoredFirstResponse =
        await playerOneConnection.InvokeAsync<ConfirmEdgeResponse>(
            "ConfirmEdge" ,
            request);

    Ensure(
        ignoredFirstResponse.IsAccepted &&
        ignoredFirstResponse.Snapshot?.Revision == 1 ,
        "응답 유실 대상으로 삼을 첫 Confirm이 정상 처리되지 않았습니다.");

    MatchSnapshot opponentSnapshot =
        await playerTwoBroadcast.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Ensure(
        opponentSnapshot.Revision == 1 &&
        opponentSnapshot.EdgeOwners[ 0 ] == PLAYER_INDEX_ENUM.PLAYER_ONE ,
        "응답 유실 중 상대 Client에 첫 Move가 전달되지 않았습니다.");

    Ensure(
        Volatile.Read(ref playerOneBroadcastCount) == 0 ,
        "응답 유실 대상 Client가 첫 Move Broadcast를 받았습니다.");

    ConfirmEdgeResponse retriedResponse =
        await playerOneConnection.InvokeAsync<ConfirmEdgeResponse>(
            "ConfirmEdge" ,
            request);

    Ensure(
        retriedResponse.IsAccepted &&
        retriedResponse.RequestId == request.RequestId &&
        retriedResponse.Snapshot?.Revision == 1 ,
        "동일 RequestId 재전송이 기존 처리 결과를 반환하지 않았습니다.");

    MatchSnapshot retriedSnapshot = retriedResponse.Snapshot ??
        throw new InvalidOperationException(
            "동일 RequestId 재전송 응답에 Snapshot이 없습니다.");

    MatchSnapshot recoveredPlayerOneSnapshot =
        await playerOneBroadcast.Task.WaitAsync(TimeSpan.FromSeconds(5));

    MatchSnapshot syncedPlayerOneSnapshot =
        await playerOneConnection.InvokeAsync<MatchSnapshot>(
            "RequestSync" ,
            developmentMatch.MatchId);

    EnsureSnapshotsEqual(
        retriedSnapshot ,
        recoveredPlayerOneSnapshot ,
        "응답 유실 후 Player 1 Broadcast 복구");

    EnsureSnapshotsEqual(
        retriedSnapshot ,
        syncedPlayerOneSnapshot ,
        "응답 유실 후 Player 1 RequestSync 복구");
}

static HubConnection CreateFullMatchConnection(
    string serverUrl ,
    string hubPath ,
    Guid userId ,
    bool simulateConfirmResponseLossOnce = false)
{
    string connectionUrl = $"{serverUrl}{hubPath}?userId={userId:D}";

    if ( simulateConfirmResponseLossOnce )
    {
        connectionUrl += "&simulateConfirmResponseLossOnce=true";
    }

    return new HubConnectionBuilder()
        .WithUrl(connectionUrl)
        .Build();
}

static TaskCompletionSource<MatchSnapshot> CreateSnapshotCompletionSource()
{
    return new TaskCompletionSource<MatchSnapshot>(
        TaskCreationOptions.RunContinuationsAsynchronously);
}

static void EnsureFinalSnapshot(MatchSnapshot snapshot)
{
    Ensure(
        snapshot.Revision == BoardTopology.EDGE_COUNT ,
        "전체 경기 종료 Revision이 Edge 개수와 일치하지 않습니다.");

    Ensure(
        snapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED ,
        "40개 Edge 확정 후 Match가 종료되지 않았습니다.");

    Ensure(
        snapshot.GameResult != GAME_RESULT_ENUM.IN_PROGRESS ,
        "전체 경기 종료 후 결과가 IN_PROGRESS입니다.");

    Ensure(
        snapshot.EdgeOwners.All(owner => owner != PLAYER_INDEX_ENUM.NONE) ,
        "전체 경기 종료 후 소유자가 없는 Edge가 있습니다.");

    Ensure(
        snapshot.BoxOwners.All(owner => owner != PLAYER_INDEX_ENUM.NONE) ,
        "전체 경기 종료 후 소유자가 없는 Box가 있습니다.");

    Ensure(
        snapshot.PlayerOneScore + snapshot.PlayerTwoScore == BoardTopology.BOX_COUNT ,
        "전체 경기 종료 점수 합계가 Box 개수와 일치하지 않습니다.");
}

static async Task RunMatchmaking_async(string serverUrl)
{
    Guid firstUserId = Guid.NewGuid();
    Guid secondUserId = Guid.NewGuid();

    await using HubConnection firstConnection =
        CreateFullMatchConnection(serverUrl , "/hubs/game" , firstUserId);

    await using HubConnection secondConnection =
        CreateFullMatchConnection(serverUrl , "/hubs/game" , secondUserId);

    TaskCompletionSource<MatchAssignment> firstMatchFound =
        new TaskCompletionSource<MatchAssignment>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    firstConnection.On<MatchAssignment>(
        "MatchFound" ,
        assignment => firstMatchFound.TrySetResult(assignment));

    await firstConnection.StartAsync();
    await secondConnection.StartAsync();

    MatchAssignment? queuedResult =
        await firstConnection.InvokeAsync<MatchAssignment?>("EnterMatchmaking");

    Ensure(queuedResult == null , "첫 번째 사용자는 매칭 대기 상태여야 합니다.");

    MatchAssignment? secondAssignment =
        await secondConnection.InvokeAsync<MatchAssignment?>("EnterMatchmaking");

    MatchAssignment firstAssignment = await firstMatchFound.Task.WaitAsync(
        TimeSpan.FromSeconds(5));

    Ensure(secondAssignment != null , "두 번째 사용자에게 Match 배정 결과가 없습니다.");
    Ensure(firstAssignment.MatchId == secondAssignment!.MatchId , "두 사용자의 MatchId가 일치하지 않습니다.");
    Ensure(firstAssignment.LocalPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE , "첫 번째 대기 사용자가 Player 1이 아닙니다.");
    Ensure(secondAssignment.LocalPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_TWO , "두 번째 대기 사용자가 Player 2가 아닙니다.");
    Ensure(firstAssignment.OpponentUserId == secondUserId , "첫 번째 사용자의 상대 UserId가 올바르지 않습니다.");
    Ensure(secondAssignment.OpponentUserId == firstUserId , "두 번째 사용자의 상대 UserId가 올바르지 않습니다.");

    MatchSnapshot firstSnapshot = await firstConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch" ,
        firstAssignment.MatchId);

    MatchSnapshot secondSnapshot = await secondConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch" ,
        secondAssignment.MatchId);

    EnsureSnapshotsEqual(firstSnapshot , secondSnapshot , "Matchmaking 초기 Snapshot");
    Ensure(firstSnapshot.MatchState == SERVER_MATCH_STATE_ENUM.ACTIVE , "자동 생성 Match가 ACTIVE가 아닙니다.");
    Ensure(firstSnapshot.TurnDeadlineUtc.HasValue , "자동 생성 Match에 턴 마감 시간이 없습니다.");
}

static async Task RunExplicitLeave_async(HttpClient httpClient , string serverUrl)
{
    using HttpResponseMessage createMatchResponse =
        await httpClient.PostAsync("/development/matches" , null);

    createMatchResponse.EnsureSuccessStatusCode();

    DevelopmentMatchResponse developmentMatch =
        await createMatchResponse.Content.ReadFromJsonAsync<DevelopmentMatchResponse>() ??
        throw new InvalidOperationException("나가기 검증용 Match 응답을 읽지 못했습니다.");

    await using HubConnection playerOneConnection = CreateFullMatchConnection(
        serverUrl ,
        developmentMatch.HubPath ,
        developmentMatch.PlayerOneUserId);

    await using HubConnection playerTwoConnection = CreateFullMatchConnection(
        serverUrl ,
        developmentMatch.HubPath ,
        developmentMatch.PlayerTwoUserId);

    TaskCompletionSource<MatchSnapshot> playerTwoResult =
        CreateSnapshotCompletionSource();

    playerTwoConnection.On<MatchSnapshot>(
        "MatchStateChanged" ,
        snapshot =>
        {
            if ( snapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED )
            {
                playerTwoResult.TrySetResult(snapshot);
            }
        });

    await playerOneConnection.StartAsync();
    await playerTwoConnection.StartAsync();

    await playerOneConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch" ,
        developmentMatch.MatchId);

    await playerTwoConnection.InvokeAsync<MatchSnapshot>(
        "JoinMatch" ,
        developmentMatch.MatchId);

    MatchSnapshot leaveSnapshot =
        await playerOneConnection.InvokeAsync<MatchSnapshot>(
            "LeaveMatch" ,
            developmentMatch.MatchId);

    MatchSnapshot opponentSnapshot = await playerTwoResult.Task.WaitAsync(
        TimeSpan.FromSeconds(5));

    EnsureSnapshotsEqual(leaveSnapshot , opponentSnapshot , "LeaveMatch 상대 결과");
    Ensure(leaveSnapshot.Revision == 1 , "LeaveMatch Revision이 정확히 한 번 증가하지 않았습니다.");
    Ensure(leaveSnapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED , "LeaveMatch 후 Match가 종료되지 않았습니다.");
    Ensure(leaveSnapshot.GameResult == GAME_RESULT_ENUM.PLAYER_TWO_WIN , "Player 1 나가기 후 Player 2 승리로 처리되지 않았습니다.");
    Ensure(leaveSnapshot.TurnDeadlineUtc == null , "LeaveMatch 종료 후 턴 마감 시간이 제거되지 않았습니다.");
}

static void EnsureSnapshotsEqual(
    MatchSnapshot expected ,
    MatchSnapshot actual ,
    string snapshotName)
{
    Ensure(expected.MatchId == actual.MatchId , $"{snapshotName}의 MatchId가 일치하지 않습니다.");
    Ensure(expected.Revision == actual.Revision , $"{snapshotName}의 Revision이 일치하지 않습니다.");
    Ensure(expected.MatchState == actual.MatchState , $"{snapshotName}의 MatchState가 일치하지 않습니다.");
    Ensure(expected.CurrentPlayerIndex == actual.CurrentPlayerIndex , $"{snapshotName}의 현재 차례가 일치하지 않습니다.");
    Ensure(expected.PlayerOneScore == actual.PlayerOneScore , $"{snapshotName}의 Player 1 점수가 일치하지 않습니다.");
    Ensure(expected.PlayerTwoScore == actual.PlayerTwoScore , $"{snapshotName}의 Player 2 점수가 일치하지 않습니다.");
    Ensure(expected.TurnDeadlineUtc == actual.TurnDeadlineUtc , $"{snapshotName}의 턴 마감 시간이 일치하지 않습니다.");
    Ensure(expected.PlayerOneTimeoutCount == actual.PlayerOneTimeoutCount , $"{snapshotName}의 Player 1 시간 초과 횟수가 일치하지 않습니다.");
    Ensure(expected.PlayerTwoTimeoutCount == actual.PlayerTwoTimeoutCount , $"{snapshotName}의 Player 2 시간 초과 횟수가 일치하지 않습니다.");
    Ensure(expected.GameResult == actual.GameResult , $"{snapshotName}의 게임 결과가 일치하지 않습니다.");
    Ensure(expected.EdgeOwners.SequenceEqual(actual.EdgeOwners) , $"{snapshotName}의 Edge 상태가 일치하지 않습니다.");
    Ensure(expected.BoxOwners.SequenceEqual(actual.BoxOwners) , $"{snapshotName}의 Box 상태가 일치하지 않습니다.");
}

internal sealed class DevelopmentMatchResponse
{
    public Guid MatchId { get; set; }
    public Guid PlayerOneUserId { get; set; }
    public Guid PlayerTwoUserId { get; set; }
    public string HubPath { get; set; } = string.Empty;
}
