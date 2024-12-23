# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 as build-env

WORKDIR /source

COPY Asahi/*.csproj Asahi/
COPY Asahi.Analyzers/*.csproj Asahi.Analyzers/

ARG TARGETARCH

RUN dotnet restore Asahi/ -a $TARGETARCH

COPY . .

RUN set -xe; \
dotnet publish Asahi/ -c Release -a $TARGETARCH -o /app; \
chmod +x /app/Asahi

FROM mcr.microsoft.com/dotnet/runtime:9.0 as runtime

WORKDIR /app

COPY --from=build-env /app .

VOLUME [ "/data" ]

ENV BOT_CONFIG_LOCATION /data/botconfig.yaml

CMD dotnet Asahi.dll
