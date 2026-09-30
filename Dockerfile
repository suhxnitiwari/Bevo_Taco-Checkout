# Builds Bevo's Tacos and runs it on Render.
# Step 1: compile and publish with the full .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/BevosTacos/BevosTacos.csproj src/BevosTacos/
RUN dotnet restore src/BevosTacos/BevosTacos.csproj
COPY src/BevosTacos/ src/BevosTacos/
RUN dotnet publish src/BevosTacos/BevosTacos.csproj -c Release -o /app --no-restore

# Step 2: run it on the smaller runtime-only image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
# Render sets $PORT; DATABASE_URL points the app at Postgres (migrations run on startup)
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-10000} exec dotnet BevosTacos.dll"]
