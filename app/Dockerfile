# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything (simpler and avoids case-sensitivity issues)
COPY . .

# Fix case-sensitivity issue: rename Shared to shared to match solution file expectation
RUN if [ -d "Shared" ] && [ ! -d "shared" ]; then mv Shared shared; fi

# Restore dependencies
RUN dotnet restore "SmartFlow.UI.sln"

# Build the solution
WORKDIR "/src"
RUN dotnet build "SmartFlow.UI.sln" -c Release

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
