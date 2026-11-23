# Krushi Prime – single container: React UI served by the .NET API
FROM node:22-alpine AS web
WORKDIR /web
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npx vite build --outDir /web/dist

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/ ./
RUN dotnet restore src/KrushiPrime.Api/KrushiPrime.Api.csproj
COPY --from=web /web/dist ./src/KrushiPrime.Api/wwwroot
RUN dotnet publish src/KrushiPrime.Api/KrushiPrime.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "KrushiPrime.Api.dll"]
