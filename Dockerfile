# Synerixis.Api — multi-stage (.NET 9)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Synerixis.sln ./
COPY Synerixis.Api/Synerixis.Api.csproj Synerixis.Api/
COPY Synerixis.Application/Synerixis.Application.csproj Synerixis.Application/
COPY Synerixis.Domain/Synerixis.Domain.csproj Synerixis.Domain/
COPY Synerixis.Infrastructure/Synerixis.Infrastructure.csproj Synerixis.Infrastructure/
RUN dotnet restore Synerixis.Api/Synerixis.Api.csproj
COPY Synerixis.Api/ Synerixis.Api/
COPY Synerixis.Application/ Synerixis.Application/
COPY Synerixis.Domain/ Synerixis.Domain/
COPY Synerixis.Infrastructure/ Synerixis.Infrastructure/
RUN dotnet publish Synerixis.Api/Synerixis.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Synerixis.Api.dll"]
