# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/EaaS.Domain/EaaS.Domain.csproj src/EaaS.Domain/
COPY src/EaaS.Shared/EaaS.Shared.csproj src/EaaS.Shared/
COPY src/EaaS.Infrastructure/EaaS.Infrastructure.csproj src/EaaS.Infrastructure/
COPY src/EaaS.WebhookProcessor/EaaS.WebhookProcessor.csproj src/EaaS.WebhookProcessor/

RUN dotnet restore src/EaaS.WebhookProcessor/EaaS.WebhookProcessor.csproj

COPY src/ src/
RUN dotnet publish src/EaaS.WebhookProcessor/EaaS.WebhookProcessor.csproj -c Release -o /app/publish 

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build /app/publish .

EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8081

HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
    CMD wget -q --spider http://localhost:8081/health || exit 1

ENTRYPOINT ["dotnet", "EaaS.WebhookProcessor.dll"]
