# FraudDetector Service Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

COPY ["PaymentGateway.sln", "./"]
COPY ["nuget.config", "./"]

COPY ["src/PaymentGateway.Domain/PaymentGateway.Domain.csproj", "src/PaymentGateway.Domain/"]
COPY ["src/PaymentGateway.Application/PaymentGateway.Application.csproj", "src/PaymentGateway.Application/"]
COPY ["src/PaymentGateway.Infrastructure/PaymentGateway.Infrastructure.csproj", "src/PaymentGateway.Infrastructure/"]
COPY ["src/services/PaymentGateway.Services.FraudDetector/PaymentGateway.Services.FraudDetector.csproj", "src/services/PaymentGateway.Services.FraudDetector/"]

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}
RUN dotnet restore "src/services/PaymentGateway.Services.FraudDetector/PaymentGateway.Services.FraudDetector.csproj"

COPY . .

RUN dotnet publish "src/services/PaymentGateway.Services.FraudDetector/PaymentGateway.Services.FraudDetector.csproj" \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false \
    -p:CycloneDdsDisableCodeGen=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

ENTRYPOINT ["dotnet", "PaymentGateway.Services.FraudDetector.dll"]
