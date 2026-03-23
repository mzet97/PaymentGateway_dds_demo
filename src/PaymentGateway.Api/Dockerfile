# ============================================
# PaymentGateway API - Multi-stage Dockerfile
# ============================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

# Copy solution and NuGet config
COPY ["PaymentGateway.sln", "./"]
COPY ["nuget.config", "./"]

# Copy project files for restore
COPY ["src/PaymentGateway.Domain/PaymentGateway.Domain.csproj", "src/PaymentGateway.Domain/"]
COPY ["src/PaymentGateway.Application/PaymentGateway.Application.csproj", "src/PaymentGateway.Application/"]
COPY ["src/PaymentGateway.Infrastructure/PaymentGateway.Infrastructure.csproj", "src/PaymentGateway.Infrastructure/"]
COPY ["src/PaymentGateway.Api/PaymentGateway.Api.csproj", "src/PaymentGateway.Api/"]

# Restore (fetches CycloneDDS.NET from GitHub Packages)
ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}
RUN dotnet restore "src/PaymentGateway.Api/PaymentGateway.Api.csproj"

# Copy source code
COPY . .

# Build and Publish
RUN dotnet publish "src/PaymentGateway.Api/PaymentGateway.Api.csproj" \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false \
    -p:CycloneDdsDisableCodeGen=true

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

EXPOSE 5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
