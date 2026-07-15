FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["src/Zero72.Blog.Client/Zero72.Blog.Client.csproj", "src/Zero72.Blog.Client/"]
COPY ["src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj", "src/Zero72.Blog.ClientHost/"]
COPY ["src/Zero72.Blog.Components/Zero72.Blog.Components.csproj", "src/Zero72.Blog.Components/"]
COPY ["src/Zero72.Blog.Reading/Zero72.Blog.Reading.csproj", "src/Zero72.Blog.Reading/"]
COPY ["src/Zero72.Blog.Shared/Zero72.Blog.Shared.csproj", "src/Zero72.Blog.Shared/"]
RUN dotnet restore "src/Zero72.Blog.Client/Zero72.Blog.Client.csproj"
RUN dotnet restore "src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj"
COPY . .
RUN dotnet publish "src/Zero72.Blog.Client/Zero72.Blog.Client.csproj" -c Release -o /app/client --no-restore
RUN dotnet publish "src/Zero72.Blog.ClientHost/Zero72.Blog.ClientHost.csproj" -c Release -o /app/host --no-restore
RUN find /app/client/wwwroot/_framework -name "blazor.webassembly*.js" -type f -print -quit | grep .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/host .
COPY --from=build /app/client/wwwroot/ /app/wwwroot/
RUN find /app/wwwroot/_framework -name "blazor.webassembly*.js" -type f -print -quit | grep .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Zero72.Blog.ClientHost.dll"]
