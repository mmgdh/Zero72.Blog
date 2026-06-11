# Zero72.Blog

Personal blog project scaffold with:

- Blazor WebAssembly client
- ASP.NET Core Web API
- Shared DTO project
- Domain model project
- EF Core infrastructure project for PostgreSQL
- Docker Compose draft for local deployment

## Project Structure

- `src/Zero72.Blog.Client`: Blazor WebAssembly frontend.
- `src/Zero72.Blog.ClientHost`: ASP.NET Core static-file host for the published WASM app.
- `src/Zero72.Blog.Api`: ASP.NET Core Web API.
- `src/Zero72.Blog.Shared`: DTOs shared by frontend and backend.
- `src/Zero72.Blog.Domain`: Domain entities.
- `src/Zero72.Blog.Infrastructure`: EF Core + PostgreSQL data access.

## Local Development

Start PostgreSQL:

```powershell
docker compose up -d postgres
```

Restore and build:

```powershell
dotnet restore Zero72.Blog.slnx
dotnet build Zero72.Blog.slnx
```

Create and apply the initial EF Core migration:

```powershell
dotnet ef migrations add InitialCreate --project src/Zero72.Blog.Infrastructure --startup-project src/Zero72.Blog.Api
dotnet ef database update --project src/Zero72.Blog.Infrastructure --startup-project src/Zero72.Blog.Api
```

Run API and client:

```powershell
dotnet run --project src/Zero72.Blog.Api
dotnet run --project src/Zero72.Blog.Client
```

## Docker

```powershell
docker compose up --build
```

Default ports:

- Client: `http://localhost:8080`
- API: `http://localhost:5000`
- PostgreSQL: `localhost:5432`

The client container uses `mcr.microsoft.com/dotnet/aspnet` to serve static files, so it does not need the Docker Hub `nginx` image.
