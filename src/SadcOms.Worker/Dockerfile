# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/SadcOms.Domain/SadcOms.Domain.csproj src/SadcOms.Domain/
COPY src/SadcOms.Contracts/SadcOms.Contracts.csproj src/SadcOms.Contracts/
COPY src/SadcOms.Application/SadcOms.Application.csproj src/SadcOms.Application/
COPY src/SadcOms.Infrastructure/SadcOms.Infrastructure.csproj src/SadcOms.Infrastructure/
COPY src/SadcOms.Worker/SadcOms.Worker.csproj src/SadcOms.Worker/
RUN dotnet restore src/SadcOms.Worker/SadcOms.Worker.csproj

COPY src/ src/
RUN dotnet publish src/SadcOms.Worker/SadcOms.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "SadcOms.Worker.dll"]
