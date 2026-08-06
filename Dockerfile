FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Unidemix.sln ./
COPY src/Unidemix.Api/Unidemix.Api.csproj src/Unidemix.Api/
RUN dotnet restore src/Unidemix.Api/Unidemix.Api.csproj
COPY src/Unidemix.Api/ src/Unidemix.Api/
RUN dotnet publish src/Unidemix.Api/Unidemix.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "Unidemix.Api.dll"]
