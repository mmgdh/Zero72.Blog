FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuGet.Config", "./"]
COPY ["src/Zero72.Blog.Api/Zero72.Blog.Api.csproj", "src/Zero72.Blog.Api/"]
COPY ["src/Zero72.Blog.Domain/Zero72.Blog.Domain.csproj", "src/Zero72.Blog.Domain/"]
COPY ["src/Zero72.Blog.Infrastructure/Zero72.Blog.Infrastructure.csproj", "src/Zero72.Blog.Infrastructure/"]
COPY ["src/Zero72.Blog.Shared/Zero72.Blog.Shared.csproj", "src/Zero72.Blog.Shared/"]
RUN dotnet restore "src/Zero72.Blog.Api/Zero72.Blog.Api.csproj"
COPY . .
RUN dotnet publish "src/Zero72.Blog.Api/Zero72.Blog.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Zero72.Blog.Api.dll"]
