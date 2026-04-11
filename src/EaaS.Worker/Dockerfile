# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/EaaS.Domain/EaaS.Domain.csproj src/EaaS.Domain/
COPY src/EaaS.Shared/EaaS.Shared.csproj src/EaaS.Shared/
COPY src/EaaS.Infrastructure/EaaS.Infrastructure.csproj src/EaaS.Infrastructure/
COPY src/EaaS.Worker/EaaS.Worker.csproj src/EaaS.Worker/

RUN dotnet restore src/EaaS.Worker/EaaS.Worker.csproj

COPY src/ src/
RUN dotnet publish src/EaaS.Worker/EaaS.Worker.csproj -c Release -o /app/publish 

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "EaaS.Worker.dll"]
