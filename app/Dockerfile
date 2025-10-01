# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["SmartFlow.UI.sln", "."]
COPY ["SmartFlow.UI.API/SmartFlow.UI.API.csproj", "SmartFlow.UI.API/"]
COPY ["SmartFlow.UI.Client/SmartFlow.UI.Client.csproj", "SmartFlow.UI.Client/"]
COPY ["Shared/Shared/Shared.csproj", "shared/Shared/"]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]

# Restore dependencies
RUN dotnet restore "SmartFlow.UI.sln"

# Copy the rest of the source code
COPY . .

# Build the solution
WORKDIR "/src"
RUN dotnet build "SmartFlow.UI.sln" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SmartFlow.UI.API/SmartFlow.UI.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartFlow.UI.API.dll"]
