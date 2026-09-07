using DotsAndBoxes.Shared;
using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Server.Authentication;
using DotsAndBoxes.Server.Hubs;
using DotsAndBoxes.Server.Matchmaking;
using DotsAndBoxes.Server.Accounts;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddGameAccounts(builder.Configuration);

builder.Services.AddSingleton<MatchRoomProvider>();
builder.Services.AddSingleton<MatchConnectionRegistry>();
builder.Services.AddSingleton<MatchRoomLifecycleService>();
builder.Services.AddSingleton<MatchmakingQueue>();
builder.Services.Configure<MatchTimingOptions>(
    builder.Configuration.GetSection(MatchTimingOptions.SECTION_NAME));
builder.Services.AddSingleton<MatchRoomFactory>();
builder.Services.AddHostedService<MatchTimerService>();
builder.Services.AddSignalR();

WebApplication app = builder.Build();

if ( app.Environment.IsDevelopment() )
{
    app.UseMiddleware<DevelopmentUserSessionMiddleware>();

    app.MapPost("/development/matches" , (
        MatchRoomProvider matchRoomProvider ,
        MatchRoomFactory matchRoomFactory ,
        ILogger<Program> logger) =>
    {
        Guid playerOneUserId = Guid.NewGuid();
        Guid playerTwoUserId = Guid.NewGuid();

        MatchPlayer playerOne =
            new MatchPlayer(playerOneUserId);

        MatchPlayer playerTwo =
            new MatchPlayer(playerTwoUserId);

        MatchRoom matchRoom = matchRoomFactory.Create(
            playerOne ,
            playerTwo ,
            PLAYER_INDEX_ENUM.PLAYER_ONE);

        if ( !matchRoomProvider.TryAdd(matchRoom) )
        {
            return Results.Conflict();
        }

        logger.LogInformation(
            "Development match created. MatchId={MatchId}, PlayerOneUserId={PlayerOneUserId}, PlayerTwoUserId={PlayerTwoUserId}",
            matchRoom.MatchId ,
            playerOne.UserId ,
            playerTwo.UserId);

        return Results.Ok(new
        {
            matchRoom.MatchId ,
            PlayerOneUserId = playerOne.UserId ,
            PlayerTwoUserId = playerTwo.UserId ,
            HubPath = "/hubs/game"
        });
    });

    app.MapGet("/development/status" , (
        MatchRoomProvider matchRoomProvider ,
        MatchConnectionRegistry matchConnectionRegistry ,
        MatchmakingQueue matchmakingQueue) =>
    {
        return Results.Ok(new
        {
            Status = "Healthy" ,
            MatchRooms = new
            {
                Total = matchRoomProvider.Count ,
                Waiting = matchRoomProvider.WaitingCount ,
                Starting = matchRoomProvider.StartingCount ,
                Active = matchRoomProvider.ActiveCount ,
                Finished = matchRoomProvider.FinishedCount ,
                Cancelled = matchRoomProvider.CancelledCount
            } ,
            ActiveMatchConnections = matchConnectionRegistry.Count ,
            MatchmakingQueueCount = matchmakingQueue.Count ,
            CheckedAtUtc = DateTimeOffset.UtcNow
        });
    });
}

app.MapGet("/health/live" , () =>
{
    return Results.Ok(new
    {
        Status = "Healthy"
    });
});

app.MapGet("/health/ready" , () =>
{
    return Results.Ok(new
    {
        Status = "Ready" ,
        CheckedAtUtc = DateTimeOffset.UtcNow
    });
});

app.MapGet("/health/core" , () =>
{
    DotsBoard board = new DotsBoard();

    return Results.Ok(new
    {
        EdgeCount = board.Edges.Count ,
        BoxCount = board.Boxes.Count ,
        ConfirmedEdgeCount = board.ConfirmedEdgeCount ,
        OwnedBoxCount = board.OwnedBoxCount ,
        CurrentPlayerIndex = board.CurrentPlayerIndex.ToString()
    });
});

app.MapHub<GameHub>("/hubs/game");

app.Run();
