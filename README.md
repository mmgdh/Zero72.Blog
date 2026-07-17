# Zero72.Blog

Personal blog project scaffold with:

- ASP.NET Core static SSR client with selective Interactive Server components
- ASP.NET Core Web API
- Shared DTO project
- Domain model project
- EF Core infrastructure project for PostgreSQL
- Docker Compose draft for local deployment

## Project Structure

- `src/Zero72.Blog.Client`: Public Razor component library rendered by the server host.
- `src/Zero72.Blog.Admin`: Blazor WebAssembly admin frontend.
- `src/Zero72.Blog.ClientHost`: ASP.NET Core SSR host for the public site and static-file host for the admin container.
- `src/Zero72.Blog.Api`: ASP.NET Core Web API.
- `src/Zero72.Blog.Shared`: DTOs shared by frontend and backend.
- `src/Zero72.Blog.Domain`: Domain entities.
- `src/Zero72.Blog.Infrastructure`: EF Core + PostgreSQL data access.
- `src/Zero72.Blog.Mobile`: Android MAUI Blazor Hybrid 博客助手，用于维护阅读记录、书籍和临时感悟。

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

Run API and the server-rendered public site:

```powershell
dotnet run --project src/Zero72.Blog.Api
dotnet run --project src/Zero72.Blog.ClientHost
```

The public host defaults to server rendering. Set `Hosting__Mode=Static` when it is used to host a standalone WebAssembly publish, as the admin container does.

Run the unified host locally after publishing the client:

```powershell
dotnet publish src/Zero72.Blog.Client -c Release -o .tmp/client-publish
dotnet publish src/Zero72.Blog.ClientHost -c Release -o .tmp/web-publish
```

## Docker

```powershell
docker compose up --build

docker compose down --remove-orphans 
docker compose build api client admin --no-cache 
docker compose up -d
```

Default ports:

- Blog client: `http://localhost:8080`
- Admin client: `http://localhost:8081`
- API: `http://localhost:5000`
- PostgreSQL: `localhost:5432`

Docker Compose starts all services together while keeping them separated: `api`, `client`, `admin`, and `postgres`.

## Windows 一键发布工具

仓库提供 WinForms 发布工具：`tools/Zero72.Blog.Deployer`。它会执行本地快速检查、安全打包、SSH 上传、远程 Docker Compose 构建、容器切换和健康检查；新版本失败时自动恢复上一套源码和镜像，不会删除 PostgreSQL 数据卷。

直接运行：

```powershell
dotnet run --project tools/Zero72.Blog.Deployer
```

生成单文件 Windows 程序：

```powershell
dotnet publish tools/Zero72.Blog.Deployer -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/deployer
```

首次使用时确认项目目录与 SSH 私钥路径，然后先点击“测试连接”。“本地格式与测试检查”默认关闭，生产镜像始终在服务器的干净 Docker 环境中强制构建。工具仅保存私钥路径，不读取或复制私钥内容；源码包会排除 `.env`、`.secrets`、Git 数据和编译缓存，生产 `.env` 与后台运行时配置始终从服务器现有版本继承。

## Android 博客助手

`src/Zero72.Blog.Mobile` 是 Android 专用的 MAUI Blazor Hybrid 应用，包含管理员登录、阅读记录、所读书籍、临时感悟、草稿和图片上传功能，不提供博文编辑。默认连接 `http://47.114.74.197:8080/`，也可以在 App 设置页修改。

首次打包会把 JDK 17 和 Android API 36 SDK 安装到当前用户目录：

```powershell
powershell -ExecutionPolicy Bypass -File tools/Build-Android.ps1
```

可安装 APK 输出到 `artifacts/mobile/Zero72.Blog.Mobile-1.0.0.apk`。应用不保存管理员密码；当前服务器使用 HTTP，因此 Android 清单暂时允许明文连接，生产环境配置 HTTPS 后应同步关闭该选项。
