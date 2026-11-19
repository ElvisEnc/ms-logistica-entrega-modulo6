# Imagen de ms-logistica-entrega (Módulo 5: integración con Gateway, Consul y RabbitMQ).
# Etapa 1: compila y publica. Etapa 2: solo el runtime de ASP.NET.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

# Primero los .csproj, para cachear el restore mientras no cambien las dependencias.
COPY src/Logistica.Domain/Logistica.Domain.csproj src/Logistica.Domain/
COPY src/Logistica.Application/Logistica.Application.csproj src/Logistica.Application/
COPY src/Logistica.Infrastructure/Logistica.Infrastructure.csproj src/Logistica.Infrastructure/
COPY src/Logistica.WebApi/Logistica.WebApi.csproj src/Logistica.WebApi/
RUN dotnet restore src/Logistica.WebApi/Logistica.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/Logistica.WebApi/Logistica.WebApi.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=builder /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Logistica.WebApi.dll"]
