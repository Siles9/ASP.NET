# ---------- Образ для сборки ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.sln ./
COPY src/ZooStav.Data/*.csproj                      src/ZooStav.Data/
COPY src/ZooStav.Web/*.csproj                       src/ZooStav.Web/
COPY src/ZooStav.Migrations.SqlServer/*.csproj      src/ZooStav.Migrations.SqlServer/
COPY src/ZooStav.Migrations.Sqlite/*.csproj         src/ZooStav.Migrations.Sqlite/
COPY tests/ZooStav.Tests/*.csproj                   tests/ZooStav.Tests/
RUN dotnet restore src/ZooStav.Web/ZooStav.Web.csproj

COPY . .
RUN dotnet publish src/ZooStav.Web/ZooStav.Web.csproj -c Release -o /app/publish --no-restore

# ---------- Образ для запуска ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Database__Provider=SqlServer

EXPOSE 8080

ENTRYPOINT ["dotnet", "ZooStav.Web.dll"]
