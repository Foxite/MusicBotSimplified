FROM mcr.microsoft.com/dotnet/runtime:6.0-bullseye-slim-amd64 AS base

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim-amd64 AS build
WORKDIR /src
RUN dotnet new tool-manifest && \
    dotnet tool install dotnet-symbol
COPY ["IkIheMusicBotSimplified.csproj", "./"]
RUN dotnet restore "IkIheMusicBotSimplified.csproj"
COPY . .
RUN dotnet build "IkIheMusicBotSimplified.csproj" -c Release -o /app/build
RUN dotnet publish "IkIheMusicBotSimplified.csproj" -c Release -o /app/publish

FROM base AS final

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y libopus0 libopus-dev libsodium23 libsodium-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
CMD ["./IkIheMusicBotSimplified"]
