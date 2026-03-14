# -----------------------------------------
# FÁZE 1: BĚHOVÉ PROSTŘEDÍ (Malý image pro běh)
# -----------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Instalace TSharku přímo do kontejneru pro parsování PCAP souborů
RUN apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y tshark && \
    rm -rf /var/lib/apt/lists/*

# -----------------------------------------
# FÁZE 2: KOMPILACE (Velké SDK pro build)
# -----------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Zkopírování projektových souborů pro efektivní využití Docker cache
COPY ["src/V2XDashboard.Server/V2XDashboard.Server.csproj", "V2XDashboard.Server/"]
COPY ["src/V2XDashboard.Client/V2XDashboard.Client.csproj", "V2XDashboard.Client/"]
COPY ["src/V2XDashboard.Shared/V2XDashboard.Shared.csproj", "V2XDashboard.Shared/"]

# Stažení závislostí
RUN dotnet restore "V2XDashboard.Server/V2XDashboard.Server.csproj"

# Zkopírování kompletních zdrojových kódů
COPY src/ .
WORKDIR "/src/V2XDashboard.Server"
RUN dotnet build "V2XDashboard.Server.csproj" -c Release -o /app/build

# -----------------------------------------
# FÁZE 3: PUBLIKOVÁNÍ (OPRAVENO PRO BLAZOR WASM)
# -----------------------------------------
FROM build AS publish
# 1. Zkompilujeme Server
WORKDIR "/src/V2XDashboard.Server"
RUN dotnet publish "V2XDashboard.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Zkompilujeme Klienta do dočasné složky
WORKDIR "/src/V2XDashboard.Client"
RUN dotnet publish "V2XDashboard.Client.csproj" -c Release -o /app/client_publish

# 3. KOUZLO: Zaručíme, že Server má složku wwwroot a překopírujeme do ní kompletní frontend (včetně _framework)
RUN mkdir -p /app/publish/wwwroot && cp -a /app/client_publish/wwwroot/. /app/publish/wwwroot/

# -----------------------------------------
# FÁZE 4: FINÁLNÍ SESTAVENÍ
# -----------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "V2XDashboard.Server.dll"]