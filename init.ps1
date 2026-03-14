New-Item -ItemType File -Name "docker-compose.yml" -Force
New-Item -ItemType File -Name "README.md" -Force
New-Item -ItemType Directory -Path "docker\web" -Force
New-Item -ItemType File -Path "docker\web\Dockerfile" -Force
New-Item -ItemType Directory -Path "docker\postgres" -Force
New-Item -ItemType File -Path "docker\postgres\init.sql" -Force
New-Item -ItemType Directory -Path "src" -Force
Set-Location "src"

# Založení projektů S PARAMETREM pro klasickou strukturu
dotnet new sln -n V2XDashboard
dotnet new classlib -n V2XDashboard.Shared -f net10.0
dotnet new blazorwasm -n V2XDashboard.Client -f net10.0 --use-program-main
dotnet new webapi -n V2XDashboard.Server -f net10.0 --use-program-main

# Propojení a přidání do Solution
dotnet add V2XDashboard.Client/V2XDashboard.Client.csproj reference V2XDashboard.Shared/V2XDashboard.Shared.csproj
dotnet add V2XDashboard.Server/V2XDashboard.Server.csproj reference V2XDashboard.Shared/V2XDashboard.Shared.csproj
dotnet add V2XDashboard.Server/V2XDashboard.Server.csproj reference V2XDashboard.Client/V2XDashboard.Client.csproj
dotnet sln V2XDashboard.sln add V2XDashboard.Shared/V2XDashboard.Shared.csproj
dotnet sln V2XDashboard.sln add V2XDashboard.Client/V2XDashboard.Client.csproj
dotnet sln V2XDashboard.sln add V2XDashboard.Server/V2XDashboard.Server.csproj

# Instalace balíčků
dotnet add V2XDashboard.Client/V2XDashboard.Client.csproj package MudBlazor
dotnet add V2XDashboard.Server/V2XDashboard.Server.csproj package Dapper
dotnet add V2XDashboard.Server/V2XDashboard.Server.csproj package Npgsql
dotnet add V2XDashboard.Server/V2XDashboard.Server.csproj package Microsoft.AspNetCore.Components.WebAssembly.Server

# Složky pro Server
New-Item -ItemType Directory -Path "V2XDashboard.Server\Controllers" -Force
New-Item -ItemType Directory -Path "V2XDashboard.Server\Data" -Force
New-Item -ItemType Directory -Path "V2XDashboard.Server\Services" -Force

Set-Location ..
Write-Host "Projekty vygenerovány nativně pomocí --use-program-main!" -ForegroundColor Green