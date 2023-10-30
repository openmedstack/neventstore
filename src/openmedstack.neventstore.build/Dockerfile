FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/openmedstack.neventstore.build/openmedstack.neventstore.build.csproj", "src/openmedstack.neventstore.build/"]
RUN dotnet restore "src/openmedstack.neventstore.build/openmedstack.neventstore.build.csproj"
COPY . .
WORKDIR "/src/src/openmedstack.neventstore.build"
RUN dotnet build "openmedstack.neventstore.build.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "openmedstack.neventstore.build.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "openmedstack.neventstore.build.dll"]
