FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["IkIheMusicBotSimplified.csproj", "./"]
RUN dotnet restore "IkIheMusicBotSimplified.csproj"
COPY . .
COPY ["appsettings.json.example", "appsettings.json"]
WORKDIR "/src/"
RUN dotnet build "IkIheMusicBotSimplified.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IkIheMusicBotSimplified.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IkIheMusicBotSimplified.dll"]
