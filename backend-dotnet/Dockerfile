FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Mozaika.Api.csproj ./
RUN dotnet restore Mozaika.Api.csproj

COPY . .
RUN dotnet publish Mozaika.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8000
EXPOSE 8000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mozaika.Api.dll"]
