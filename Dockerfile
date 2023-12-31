FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./Liberex/Liberex.csproj
RUN dotnet publish -c release -o /app ./Liberex/Liberex.csproj

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Liberex.dll"]
