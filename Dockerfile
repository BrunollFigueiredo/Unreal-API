FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Unreal_PI/Unreal_PI.csproj Unreal_PI/
COPY deploy/003-receitas-bugigangas.sql deploy/
RUN dotnet restore Unreal_PI/Unreal_PI.csproj
COPY Unreal_PI/ Unreal_PI/
RUN dotnet publish Unreal_PI/Unreal_PI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
CMD ["/bin/sh", "-c", "exec dotnet Unreal_PI.dll --urls http://0.0.0.0:${PORT:-8080}"]
