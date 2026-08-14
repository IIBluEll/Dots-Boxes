using DotsAndBoxes.Shared;
using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Server.Authentication;
using DotsAndBoxes.Server.Hubs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MatchRoomProvider>();
builder.Services.AddSignalR();

WebApplication app = builder.Build();

if ( app.Environment.IsDevelopment() )
{
    app.UseMiddleware<DevelopmentUserSessionMiddleware>();
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