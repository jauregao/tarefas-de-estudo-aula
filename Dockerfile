FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY back/Tarefas.Api.csproj back/
RUN dotnet restore back/Tarefas.Api.csproj

COPY back/ back/
RUN dotnet publish back/Tarefas.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 10000
ENTRYPOINT ["sh", "-c", "dotnet Tarefas.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
