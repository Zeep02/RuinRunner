# Remote deployment recipe; not built or publicly deployed on this Windows host.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY RuinRunner.Api/ RuinRunner.Api/
COPY Content/ Content/
RUN dotnet restore RuinRunner.Api/RuinRunner.Api.csproj --locked-mode
RUN dotnet publish RuinRunner.Api/RuinRunner.Api.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RuinRunner.Api.dll"]
