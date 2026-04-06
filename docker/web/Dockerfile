FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ARG TSHARK_VERSION=4.6.4

RUN apt-get update && \
        DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
            ca-certificates \
            gnupg \
            software-properties-common && \
        add-apt-repository -y ppa:wireshark-dev/stable && \
        apt-get update && \
        DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends tshark && \
        tshark -v | head -n 1 | grep -E "TShark \(Wireshark\) ${TSHARK_VERSION}" && \
        apt-get purge -y --auto-remove software-properties-common && \
    rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/V2XDashboard.Server/V2XDashboard.Server.csproj", "V2XDashboard.Server/"]
COPY ["src/V2XDashboard.Client/V2XDashboard.Client.csproj", "V2XDashboard.Client/"]
COPY ["src/V2XDashboard.Shared/V2XDashboard.Shared.csproj", "V2XDashboard.Shared/"]

RUN dotnet restore "V2XDashboard.Server/V2XDashboard.Server.csproj"

COPY src/ .
WORKDIR "/src/V2XDashboard.Server"
RUN dotnet build "V2XDashboard.Server.csproj" -c Release -o /app/build

FROM build AS publish

WORKDIR "/src/V2XDashboard.Server"
RUN dotnet publish "V2XDashboard.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

WORKDIR "/src/V2XDashboard.Client"
RUN dotnet publish "V2XDashboard.Client.csproj" -c Release -o /app/client_publish

RUN mkdir -p /app/publish/wwwroot && cp -a /app/client_publish/wwwroot/. /app/publish/wwwroot/

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "V2XDashboard.Server.dll"]