# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 as build-env

WORKDIR /source

COPY Seigen/*.csproj Seigen/

ARG TARGETARCH

RUN dotnet restore Seigen/ -a $TARGETARCH

COPY . .

RUN set -xe; \
dotnet publish -c Release -a $TARGETARCH -o /app; \
chmod +x /app/Seigen

FROM mcr.microsoft.com/dotnet/runtime:8.0 as runtime

WORKDIR /app

COPY --from=build-env /app .

VOLUME [ "/data" ]

ENV BOT_CONFIG_LOCATION /data/botconfig.yaml

CMD dotnet Seigen.dll
