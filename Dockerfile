FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RockPaperScissors/RockPaperScissors.csproj", "RockPaperScissors/"]
RUN dotnet restore "RockPaperScissors/RockPaperScissors.csproj"
COPY . .
WORKDIR "/src/RockPaperScissors"
RUN dotnet build "RockPaperScissors.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RockPaperScissors.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RockPaperScissors.dll"]
