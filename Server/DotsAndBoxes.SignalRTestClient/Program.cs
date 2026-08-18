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

internal sealed class DevelopmentMatchResponse
{
    public Guid MatchId { get; set; }
    public Guid PlayerOneUserId { get; set; }
    public Guid PlayerTwoUserId { get; set; }
    public string HubPath { get; set; } = string.Empty;
}
