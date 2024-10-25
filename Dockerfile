FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:35792ea4ad1db051981f62b313f1be3b46b1f45cadbaa3c288cd0d3056eefb83 AS build-env
WORKDIR /Octobot

# Copy everything
COPY . ./
# Load build argument with publish options
ARG PUBLISH_OPTIONS="-c Release"
# Build and publish a release
RUN dotnet publish ./TeamOctolings.Octobot/TeamOctolings.Octobot.csproj $PUBLISH_OPTIONS -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0@sha256:a335dccd3231f7f9e2122691b21c634f96e187d3840c8b7dbad61ee09500e408
WORKDIR /Octobot
COPY --from=build-env /Octobot/out .
ENTRYPOINT ["./TeamOctolings.Octobot"]
