FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files for layer caching
COPY src/AiDemo.Domain/AiDemo.Domain.csproj AiDemo.Domain/
COPY src/AiDemo.Contracts/AiDemo.Contracts.csproj AiDemo.Contracts/
COPY src/AiDemo.Application/AiDemo.Application.csproj AiDemo.Application/
COPY src/AiDemo.Infrastructure/AiDemo.Infrastructure.csproj AiDemo.Infrastructure/
COPY src/AiDemo.Server/AiDemo.Server.csproj AiDemo.Server/
COPY NuGet.Config ./

# Restore dependencies
RUN dotnet restore AiDemo.Server/AiDemo.Server.csproj

# Copy source code
COPY src/AiDemo.Domain/ AiDemo.Domain/
COPY src/AiDemo.Contracts/ AiDemo.Contracts/
COPY src/AiDemo.Application/ AiDemo.Application/
COPY src/AiDemo.Infrastructure/ AiDemo.Infrastructure/
COPY src/AiDemo.Server/ AiDemo.Server/

# Build and publish
RUN dotnet publish AiDemo.Server/AiDemo.Server.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "AiDemo.Server.dll"]
