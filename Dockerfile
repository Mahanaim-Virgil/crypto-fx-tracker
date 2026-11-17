# syntax=docker/dockerfile:1

# ---- Derleme aşaması: SDK imajı (derleyici, NuGet) yalnızca burada ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: kod değişip paketler değişmediğinde restore katmanı önbellekten gelir.
COPY src/CryptoFxTracker.Domain/CryptoFxTracker.Domain.csproj src/CryptoFxTracker.Domain/
COPY src/CryptoFxTracker.Application/CryptoFxTracker.Application.csproj src/CryptoFxTracker.Application/
COPY src/CryptoFxTracker.Infrastructure/CryptoFxTracker.Infrastructure.csproj src/CryptoFxTracker.Infrastructure/
COPY src/CryptoFxTracker.Api/CryptoFxTracker.Api.csproj src/CryptoFxTracker.Api/
RUN dotnet restore src/CryptoFxTracker.Api/CryptoFxTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/CryptoFxTracker.Api/CryptoFxTracker.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---- Çalışma aşaması: yalnızca ASP.NET Core çalışma zamanı ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Resmi .NET 8 imajlarındaki yetkisiz "app" kullanıcısı; container root olarak çalışmaz.
USER $APP_UID

# .NET 8 imajları varsayılan olarak 8080'i dinler (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080
ENTRYPOINT ["dotnet", "CryptoFxTracker.Api.dll"]
