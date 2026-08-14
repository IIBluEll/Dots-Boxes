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

Console.WriteLine("SignalR 통합 검증 성공");
Console.WriteLine($"MatchId: {developmentMatch.MatchId}");
Console.WriteLine("Revision: 0 → 1");
Console.WriteLine("Player 1 Broadcast 수신: 성공");
Console.WriteLine("Player 2 Broadcast 수신: 성공");
Console.WriteLine("Player 2 RequestSync 검증: 성공");

static HubConnection CreateConnection(
    string hubPath ,
    Guid userId)
{
    string connectionUrl =
        $"{SERVER_URL}{hubPath}?userId={userId:D}";

    return new HubConnectionBuilder()
        .WithUrl(connectionUrl)
        .WithAutomaticReconnect()
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