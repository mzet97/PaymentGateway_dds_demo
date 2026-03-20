# ============================================
# PaymentGateway API - Multi-stage Dockerfile
# ============================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

# Install CycloneDDS build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential cmake libssl-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy solution, NuGet config, and local packages
COPY ["PaymentGateway.sln", "./"]
COPY ["nuget.config", "./"]
COPY ["artifacts/nuget/", "artifacts/nuget/"]

# Copy project files for restore
COPY ["src/PaymentGateway.Domain/PaymentGateway.Domain.csproj", "src/PaymentGateway.Domain/"]
COPY ["src/PaymentGateway.Application/PaymentGateway.Application.csproj", "src/PaymentGateway.Application/"]
COPY ["src/PaymentGateway.Infrastructure/PaymentGateway.Infrastructure.csproj", "src/PaymentGateway.Infrastructure/"]
COPY ["src/PaymentGateway.Api/PaymentGateway.Api.csproj", "src/PaymentGateway.Api/"]

# Restore dependencies
RUN dotnet restore "src/PaymentGateway.Api/PaymentGateway.Api.csproj"

# Copy all source code and native artifacts (idlc needed for code gen)
COPY . .

# Make idlc available for CycloneDDS source generator
RUN chmod +x artifacts/native/linux-x64/idlc 2>/dev/null || true
ENV PATH="/src/artifacts/native/linux-x64:${PATH}"
ENV LD_LIBRARY_PATH="/src/artifacts/native/linux-x64"

# Build and Publish
RUN dotnet publish "src/PaymentGateway.Api/PaymentGateway.Api.csproj" \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Copy native CycloneDDS libraries
COPY ["artifacts/native/linux-x64/libddsc.so*", "/usr/local/lib/"]
COPY ["artifacts/native/linux-x64/libcycloneddsidl.so*", "/usr/local/lib/"]
RUN ldconfig

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

EXPOSE 5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV LD_LIBRARY_PATH=/usr/local/lib

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
