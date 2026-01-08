FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY LittleHelperAI.API/*.csproj LittleHelperAI.API/
COPY LittleHelperAI.Agents/*.csproj LittleHelperAI.Agents/
COPY LittleHelperAI.Data/*.csproj LittleHelperAI.Data/
COPY LittleHelperAI.Workers/*.csproj LittleHelperAI.Workers/
COPY LittleHelperAI.sln .

RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet publish LittleHelperAI.API/LittleHelperAI.API.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser

COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:8001/api/health || exit 1

EXPOSE 8001
ENV ASPNETCORE_URLS=http://+:8001

ENTRYPOINT ["dotnet", "LittleHelperAI.API.dll"]
