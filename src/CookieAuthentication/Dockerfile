# Built from the solution root so the COPY paths below match the repo layout.
# See docker-compose.yml, which sets `context: .` for exactly this reason.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Restore before copying the rest of the source, so a code-only change does not invalidate
# the (slow) restore layer.
COPY ["src/CookieAuthentication/CookieAuthentication.csproj", "src/CookieAuthentication/"]
RUN dotnet restore "./src/CookieAuthentication/CookieAuthentication.csproj"
COPY . .
WORKDIR "/src/src/CookieAuthentication"
RUN dotnet build "./CookieAuthentication.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CookieAuthentication.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CookieAuthentication.dll"]
