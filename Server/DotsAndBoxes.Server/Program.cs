using DotsAndBoxes.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

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

app.Run();