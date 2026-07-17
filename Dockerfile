# Build context is the solution root (ERental.slnx). Run from there:
#   docker build -t erental-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching) — copy only project files
COPY ERental/ERental.csproj ERental/
COPY ERental.Application/ERental.Application.csproj ERental.Application/
COPY ERental.Domain/ERental.Domain.csproj ERental.Domain/
COPY ERental.Infrastructure/ERental.Infrastructure.csproj ERental.Infrastructure/
RUN dotnet restore ERental/ERental.csproj

# Now copy the rest of the source and publish
COPY ERental/ ERental/
COPY ERental.Application/ ERental.Application/
COPY ERental.Domain/ ERental.Domain/
COPY ERental.Infrastructure/ ERental.Infrastructure/
RUN dotnet publish ERental/ERental.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql's SSL/SCRAM negotiation against hosted Postgres (e.g. Neon) needs libgssapi,
# which isn't included in this base image by default.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# All secrets (ConnectionStrings__DefaultConnection, Jwt__Key, SendGrid__ApiKey, R2__*)
# are provided as environment variables at runtime — see docker-compose.yml / .env.example.
# Nothing sensitive is baked into this image.
ENTRYPOINT ["dotnet", "ERental.dll"]
