FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/GuildArena.Api/GuildArena.Api.csproj", "src/GuildArena.Api/"]
COPY ["src/GuildArena.Application/GuildArena.Application.csproj", "src/GuildArena.Application/"]
COPY ["src/GuildArena.Core/GuildArena.Core.csproj", "src/GuildArena.Core/"]
COPY ["src/GuildArena.Domain/GuildArena.Domain.csproj", "src/GuildArena.Domain/"]
COPY ["src/GuildArena.Infrastructure/GuildArena.Infrastructure.csproj", "src/GuildArena.Infrastructure/"]
COPY ["src/GuildArena.Shared/GuildArena.Shared.csproj", "src/GuildArena.Shared/"]
RUN dotnet restore "src/GuildArena.Api/GuildArena.Api.csproj"
COPY . .
WORKDIR "/src/src/GuildArena.Api"
RUN dotnet build "GuildArena.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GuildArena.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GuildArena.Api.dll"]