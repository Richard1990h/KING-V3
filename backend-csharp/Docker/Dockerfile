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
RUN dotnet publish LittleHelperAI.API/LittleHelperAI.API.csproj -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8001
ENTRYPOINT ["dotnet", "LittleHelperAI.API.dll"]
