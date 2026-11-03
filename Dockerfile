# syntax=docker/dockerfile:1

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia só os .csproj da cadeia da API (Api -> Infrastructure -> Application -> Domain)
# para aproveitar o cache de camadas do restore.
COPY src/InvestProMax.Domain/InvestProMax.Domain.csproj                 src/InvestProMax.Domain/
COPY src/InvestProMax.Application/InvestProMax.Application.csproj       src/InvestProMax.Application/
COPY src/InvestProMax.Infrastructure/InvestProMax.Infrastructure.csproj src/InvestProMax.Infrastructure/
COPY src/InvestProMax.Api/InvestProMax.Api.csproj                       src/InvestProMax.Api/
RUN dotnet restore src/InvestProMax.Api/InvestProMax.Api.csproj

# Código-fonte (filtrado pelo .dockerignore)
COPY src/InvestProMax.Domain/         src/InvestProMax.Domain/
COPY src/InvestProMax.Application/    src/InvestProMax.Application/
COPY src/InvestProMax.Infrastructure/ src/InvestProMax.Infrastructure/
COPY src/InvestProMax.Api/            src/InvestProMax.Api/

RUN dotnet publish src/InvestProMax.Api/InvestProMax.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Sem secrets na imagem: connection strings, JWT etc. entram por variáveis de ambiente.
# ASPNETCORE_FORWARDEDHEADERS_ENABLED: o Render termina o HTTPS no proxy; sem isso o
# UseHttpsRedirection entraria em loop por não enxergar o X-Forwarded-Proto.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true

# Roda como usuário não-root (já existe na imagem oficial).
USER $APP_UID

# O Render injeta PORT em runtime; fora do Render cai em 8080.
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet InvestProMax.Api.dll"]

