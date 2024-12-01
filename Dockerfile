FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:7d24e90a392e88eb56093e4eb325ff883ad609382a55d42f17fd557b997022ca AS build-env
WORKDIR /Octobot

# Copy everything
COPY . ./
# Load build argument with publish options
ARG PUBLISH_OPTIONS="-c Release"
# Build and publish a release
RUN dotnet publish ./TeamOctolings.Octobot $PUBLISH_OPTIONS -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0@sha256:1e5eb0ed94ca96a34a914456db80e48bd1bb7bc3e3c8eda5e2c3d89c153c3081
WORKDIR /Octobot
COPY --from=build-env /Octobot/out .
ENTRYPOINT ["./TeamOctolings.Octobot"]
