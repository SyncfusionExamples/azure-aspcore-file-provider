FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

RUN apt-get update -y && apt-get install fontconfig -y
WORKDIR /app


ENV SYNCFUSION_LICENSE_KEY=""
ENV AZURE_ACCOUNT_NAME=""
ENV AZURE_ACCOUNT_KEY=""
ENV AZURE_BLOB_NAME=""
ENV AZURE_BLOB_PATH=""

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source
COPY ["src/FileManager.AzureFileProvider.AspNet.Core/FileManager.AzureFileProvider.AspNet.Core.csproj", "./FileManager.AzureFileProvider.AspNet.Core/"]
COPY ["src/FileManager.AzureFileProvider.AspNet.Core/NuGet.Config", "./FileManager.AzureFileProvider.AspNet.Core/"]
RUN dotnet restore "./FileManager.AzureFileProvider.AspNet.Core/FileManager.AzureFileProvider.AspNet.Core.csproj"
COPY . .
WORKDIR "/source/src"
RUN dotnet build -c Release -f net10.0 -o /app

FROM build AS publish
RUN dotnet publish -c Release -f net10.0 -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_HTTP_PORTS=80
ENTRYPOINT ["dotnet", "FileManager.AzureFileProvider.dll"]
