# Multi-stage Dockerfile for MemoryMonitor (.NET 8)
# Usage:
#   docker build -t memmon:latest .
#   docker run --rm -p 9095:9095 memmon:latest

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./src/ ./
RUN dotnet publish MemoryMonitor.csproj -c Release -r linux-x64 -o /app/publish --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 9095
ENTRYPOINT ["./MemoryMonitor", "1", "--metrics", "0.0.0.0:9095"]
