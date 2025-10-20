# Get build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-stage
WORKDIR /app

# Copy source
COPY . ./

# Publish
RUN dotnet publish -c Release -o "/app/publish/" --disable-parallel

# Get runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS publish-stage
WORKDIR /app

RUN apt-get update --allow-releaseinfo-change \
&& apt-get install -y --allow-unauthenticated --allow-releaseinfo-change \
    libc6-dev \
    libgdiplus \
    libx11-dev \
 && rm -rf /var/lib/apt/lists/*

# Bring in metadata via --build-arg
ARG BRANCH=unknown
ARG IMAGE_CREATED=unknown
ARG IMAGE_REVISION=unknown
ARG IMAGE_VERSION=unknown

# Configure image labels
LABEL branch=$branch \
    org.opencontainers.image.created=$IMAGE_CREATED \
    org.opencontainers.image.description="Discord Ian is a bot for Discord" \
    org.opencontainers.image.documentation="https://github.com/k7hpn/discordian/" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.revision=$IMAGE_REVISION \
    org.opencontainers.image.source="https://github.com/k7hpn/discordian/" \
    org.opencontainers.image.title="DiscordIan" \
    org.opencontainers.image.url="https://github.com/k7hpn/discordian/" \
    org.opencontainers.image.version=$IMAGE_VERSION

# Default image environment variable settings
ENV org.opencontainers.image.created=$IMAGE_CREATED \
    org.opencontainers.image.revision=$IMAGE_REVISION \
    org.opencontainers.image.version=$IMAGE_VERSION

# Copy source
COPY --from=build-stage "/app/publish/" .

# Set entrypoint
ENTRYPOINT ["dotnet", "DiscordIan.dll"]
