# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore using only the files that affect the dependency graph for better layer caching.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/SadcOms.Domain/SadcOms.Domain.csproj src/SadcOms.Domain/
COPY src/SadcOms.Contracts/SadcOms.Contracts.csproj src/SadcOms.Contracts/
COPY src/SadcOms.Application/SadcOms.Application.csproj src/SadcOms.Application/
COPY src/SadcOms.Infrastructure/SadcOms.Infrastructure.csproj src/SadcOms.Infrastructure/
COPY src/SadcOms.Api/SadcOms.Api.csproj src/SadcOms.Api/
RUN dotnet restore src/SadcOms.Api/SadcOms.Api.csproj

# Copy the rest and publish.
COPY src/ src/
RUN dotnet publish src/SadcOms.Api/SadcOms.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SadcOms.Api.dll"]
