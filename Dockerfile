# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS build-env

WORKDIR /source

COPY global.json .
COPY Asahi/*.csproj Asahi/
COPY Asahi.Analyzers/*.csproj Asahi.Analyzers/
COPY Asahi.BotEmoteManagement/*.csproj Asahi.BotEmoteManagement/
COPY Asahi.BotEmoteManagement.SourceGenerators/*.csproj Asahi.BotEmoteManagement.SourceGenerators/
COPY Asahi.CodeFixers/*.csproj Asahi.CodeFixers/
COPY Asahi.Database/*.csproj Asahi.Database/
COPY Asahi.Database.Migrations.Postgres/*.csproj Asahi.Database.Migrations.Postgres/
COPY Asahi.Database.Migrations.Sqlite/*.csproj Asahi.Database.Migrations.Sqlite/

ARG TARGETARCH

RUN dotnet restore Asahi/ -a "$TARGETARCH" -p:Configuration=Release -p:UseAppHost=false

COPY . .

RUN dotnet publish Asahi/ -c Release -a "$TARGETARCH" --no-restore -p:UseAppHost=false -o /app

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS runtime

WORKDIR /app

COPY --from=build-env /app .

RUN install -d -o "$APP_UID" -g "$APP_UID" /data

VOLUME [ "/data" ]

ENV BOT_CONFIG_LOCATION="/data/botconfig.yaml" \
    BOT_SQLITE_LOCATION="/data/BotDb.db"

USER $APP_UID

ENTRYPOINT [ "dotnet", "Asahi.dll" ]
