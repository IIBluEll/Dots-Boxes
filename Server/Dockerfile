FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Server/DotsAndBoxes.Server/DotsAndBoxes.Server.csproj", "Server/DotsAndBoxes.Server/"]
COPY ["Server/DotsAndBoxes.Shared/DotsAndBoxes.Shared.csproj", "Server/DotsAndBoxes.Shared/"]

RUN dotnet restore "Server/DotsAndBoxes.Server/DotsAndBoxes.Server.csproj"

COPY ["Server/DotsAndBoxes.Server/", "Server/DotsAndBoxes.Server/"]
COPY ["Server/DotsAndBoxes.Shared/", "Server/DotsAndBoxes.Shared/"]
COPY ["Assets/01_Main/02_Scripts/Shared/", "Assets/01_Main/02_Scripts/Shared/"]

RUN dotnet publish \
    "Server/DotsAndBoxes.Server/DotsAndBoxes.Server.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "DotsAndBoxes.Server.dll"]