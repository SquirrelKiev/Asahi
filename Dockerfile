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

RUN dotnet restore Asahi/ -a $TARGETARCH

COPY . .

RUN set -xe; \
dotnet publish Asahi/ -c Release -a $TARGETARCH -o /app; \
chmod +x /app/Asahi

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS runtime

WORKDIR /app

COPY --from=build-env /app .

VOLUME [ "/data" ]

ENV BOT_CONFIG_LOCATION="/data/botconfig.yaml"

ENTRYPOINT [ "dotnet", "Asahi.dll" ]
