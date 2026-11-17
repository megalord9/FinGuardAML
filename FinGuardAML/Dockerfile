# FinGuard AML / Практическая работа №4 — контейнер .NET 8
# Поместить в папку, где находится FinGuardAML.csproj.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FinGuardAML.csproj ./
RUN dotnet restore FinGuardAML.csproj
COPY . ./
RUN dotnet publish FinGuardAML.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "FinGuardAML.dll"]
