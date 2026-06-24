FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["EasyHMSAPI.Api/EasyHMSAPI.Api.csproj", "EasyHMSAPI.Api/"]
COPY ["EasyHMSAPI.Application/EasyHMSAPI.Application.csproj", "EasyHMSAPI.Application/"]
COPY ["EasyHMSAPI.Domain/EasyHMSAPI.Domain.csproj", "EasyHMSAPI.Domain/"]
COPY ["EasyHMSAPI.Data/EasyHMSAPI.Data.csproj", "EasyHMSAPI.Data/"]
RUN dotnet restore "EasyHMSAPI.Api/EasyHMSAPI.Api.csproj"
COPY . .
WORKDIR "/src/EasyHMSAPI.Api"
RUN dotnet build "EasyHMSAPI.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EasyHMSAPI.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EasyHMSAPI.Api.dll"]
