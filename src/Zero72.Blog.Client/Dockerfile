FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["src/Zero72.Blog.Client/Zero72.Blog.Client.csproj", "src/Zero72.Blog.Client/"]
COPY ["src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj", "src/Zero72.Blog.ClientHost/"]
COPY ["src/Zero72.Blog.Components/Zero72.Blog.Components.csproj", "src/Zero72.Blog.Components/"]
COPY ["src/Zero72.Blog.Reading/Zero72.Blog.Reading.csproj", "src/Zero72.Blog.Reading/"]
COPY ["src/Zero72.Blog.Shared/Zero72.Blog.Shared.csproj", "src/Zero72.Blog.Shared/"]
RUN dotnet restore "src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj"
COPY . .
RUN dotnet restore "src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj" --force-evaluate
RUN dotnet publish "src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj" -c Release -o /app/host --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/host .
RUN test -f /app/wwwroot/css/app.css && test -f /app/wwwroot/_framework/blazor.web.js
EXPOSE 8080
ENTRYPOINT ["dotnet", "Zero72.Blog.ClientHost.dll"]
