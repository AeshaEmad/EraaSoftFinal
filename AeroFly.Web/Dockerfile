# Use the official .NET 8 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AeroFly.Web/AeroFly.Web.csproj", "AeroFly.Web/"]
RUN dotnet restore "AeroFly.Web/AeroFly.Web.csproj"
COPY . .
WORKDIR "/src/AeroFly.Web"
RUN dotnet build "AeroFly.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AeroFly.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AeroFly.Web.dll"]