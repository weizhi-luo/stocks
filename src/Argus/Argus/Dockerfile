#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80:5000
EXPOSE 443:5001

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Argus/Argus.csproj", "Argus/"]
RUN dotnet restore "Argus/Argus.csproj"
COPY . .
WORKDIR "/src/Argus"
RUN dotnet build "Argus.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Argus.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Argus.dll"]