FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/openmedstack.neventstore.server/openmedstack.neventstore.server.csproj", "openmedstack.neventstore.server/"]
RUN dotnet restore "src/openmedstack.neventstore.server/openmedstack.neventstore.server.csproj"
COPY . .
WORKDIR "/src/openmedstack.neventstore.server"
RUN dotnet build "openmedstack.neventstore.server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "openmedstack.neventstore.server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "openmedstack.neventstore.server.dll"]
