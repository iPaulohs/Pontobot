# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Pontobot.csproj", "./"]
RUN dotnet restore "Pontobot.csproj"

COPY . .
RUN dotnet publish "Pontobot.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false


# ---------- RUNTIME ----------
FROM mcr.microsoft.com/playwright/dotnet:v1.58.0-jammy AS final

WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Instalar os navegadores do Playwright usando o script que já existe
RUN pwsh ./playwright.ps1 install chromium

EXPOSE 8080

ENTRYPOINT ["dotnet", "Pontobot.dll"]