using DotsAndBoxes.Shared;
using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Server.Authentication;
using DotsAndBoxes.Server.Hubs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MatchRoomProvider>();
builder.Services.AddSingleton<MatchConnectionRegistry>();
builder.Services.AddSignalR();

WebApplication app = builder.Build();

if ( app.Environment.IsDevelopment() )
{
    app.UseMiddleware<DevelopmentUserSessionMiddleware>();

    app.MapPost("/development/matches" , (MatchRoomProvider matchRoomProvider) =>
    {
        Guid playerOneUserId = Guid.NewGuid();
        Guid playerTwoUserId = Guid.NewGuid();

        MatchPlayer playerOne =
            new MatchPlayer(playerOneUserId);

        MatchPlayer playerTwo =
            new MatchPlayer(playerTwoUserId);

        MatchRoom matchRoom = new MatchRoom(
            Guid.NewGuid(),
            playerOne,
            playerTwo,
            PLAYER_INDEX_ENUM.PLAYER_ONE);

        if ( !matchRoomProvider.TryAdd(matchRoom) )
        {
            return Results.Conflict();
        }

        return Results.Ok(new
        {
            matchRoom.MatchId ,
            PlayerOneUserId = playerOne.UserId ,
            PlayerTwoUserId = playerTwo.UserId ,
            HubPath = "/hubs/game"
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
