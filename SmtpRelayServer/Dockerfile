FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /source
COPY . .
RUN dotnet restore -a $TARGETARCH
RUN dotnet publish -a $TARGETARCH -o /app

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
EXPOSE 25 465 587
RUN mkdir /data
VOLUME /data
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./SmtpRelayServer", "--docker"]