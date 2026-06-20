FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csprojs para aprovechar la cache de restore
COPY WidexPresupuestos.Api/WidexPresupuestos.Api.csproj WidexPresupuestos.Api/
COPY WidexPresupuestos.Shared/WidexPresupuestos.Shared.csproj WidexPresupuestos.Shared/
RUN dotnet restore WidexPresupuestos.Api/WidexPresupuestos.Api.csproj

# Copiar el resto y publicar
COPY . .
RUN dotnet publish WidexPresupuestos.Api/WidexPresupuestos.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker
ENTRYPOINT ["dotnet", "WidexPresupuestos.Api.dll"]
