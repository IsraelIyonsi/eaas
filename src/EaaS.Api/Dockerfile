# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution and project files for restore
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/EaaS.Domain/EaaS.Domain.csproj src/EaaS.Domain/
COPY src/EaaS.Shared/EaaS.Shared.csproj src/EaaS.Shared/
COPY src/EaaS.Infrastructure/EaaS.Infrastructure.csproj src/EaaS.Infrastructure/
COPY src/EaaS.Api/EaaS.Api.csproj src/EaaS.Api/

RUN dotnet restore src/EaaS.Api/EaaS.Api.csproj

# Copy all source and publish
COPY src/ src/
RUN dotnet publish src/EaaS.Api/EaaS.Api.csproj -c Release -o /app/publish 

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "EaaS.Api.dll"]
