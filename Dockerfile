FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/IntegrationHub.Api/IntegrationHub.Api.csproj
RUN dotnet publish src/IntegrationHub.Api/IntegrationHub.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R "$APP_UID:$APP_UID" /home/app/.aspnet
USER $APP_UID
ENTRYPOINT ["dotnet", "IntegrationHub.Api.dll"]
