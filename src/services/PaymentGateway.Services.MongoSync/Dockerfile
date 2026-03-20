# MongoSync Service Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential cmake libssl-dev \
    && rm -rf /var/lib/apt/lists/*

COPY ["PaymentGateway.sln", "./"]
COPY ["nuget.config", "./"]
COPY ["artifacts/nuget/", "artifacts/nuget/"]

COPY ["src/PaymentGateway.Domain/PaymentGateway.Domain.csproj", "src/PaymentGateway.Domain/"]
COPY ["src/PaymentGateway.Application/PaymentGateway.Application.csproj", "src/PaymentGateway.Application/"]
COPY ["src/PaymentGateway.Infrastructure/PaymentGateway.Infrastructure.csproj", "src/PaymentGateway.Infrastructure/"]
COPY ["src/services/PaymentGateway.Services.MongoSync/PaymentGateway.Services.MongoSync.csproj", "src/services/PaymentGateway.Services.MongoSync/"]

RUN dotnet restore "src/services/PaymentGateway.Services.MongoSync/PaymentGateway.Services.MongoSync.csproj"

COPY . .

# Enable idlc for CycloneDDS source generator
RUN chmod +x artifacts/native/linux-x64/idlc 2>/dev/null || true
ENV PATH="/src/artifacts/native/linux-x64:${PATH}"
ENV LD_LIBRARY_PATH="/src/artifacts/native/linux-x64"

RUN dotnet publish "src/services/PaymentGateway.Services.MongoSync/PaymentGateway.Services.MongoSync.csproj" \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY ["artifacts/native/linux-x64/libddsc.so*", "/usr/local/lib/"]
COPY ["artifacts/native/linux-x64/libcycloneddsidl.so*", "/usr/local/lib/"]
RUN ldconfig

RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

ENV LD_LIBRARY_PATH=/usr/local/lib
ENTRYPOINT ["dotnet", "PaymentGateway.Services.MongoSync.dll"]
