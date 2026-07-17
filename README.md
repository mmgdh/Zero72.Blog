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

发布器支持“登录密码”和“SSH 私钥”两种认证方式。登录密码仅保留在当前程序运行内存中，不会写入配置文件、命令行或发布日志；首次密码连接会记录 ECS 主机指纹，后续发现指纹变化时会拒绝连接。密码模式使用带 SHA-256 校验的短连接分块上传，连接中断时只重试当前分块。管理员账号需要是 `root`，或已经具备免密 `sudo docker` 权限。

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

也可以直接打开博客部署程序，点击“生成升级 APK”。部署程序会在构建成功后自动递增显示版本和 Android 构建号，并生成：

- `artifacts/mobile/Zero72.Blog.Mobile-<版本>.apk`：可直接发送或安装的版本化 APK。
- `artifacts/mobile/Zero72.Blog.Mobile-latest.apk`：始终指向本次构建的稳定文件名副本。
- `src/Zero72.Blog.ClientHost/wwwroot/mobile/latest.json`：安卓端使用的远程升级清单。
- `src/Zero72.Blog.ClientHost/wwwroot/mobile/Zero72.Blog.Mobile-<版本>.apk`：随博客容器发布的下载文件。

生成后继续点击“开始一键发布”。安卓应用每次启动会读取博客服务器的 `/mobile/latest.json`，当服务器构建号更高时弹出更新提示；设置页也支持手动检查。点击更新会调用系统浏览器下载 APK，Android 仍会要求用户确认安装及“允许来自此来源的应用”，应用不会绕过系统进行静默安装。

远程升级要求所有版本使用同一 Android 签名。当前电脑的默认签名密钥需要妥善备份；如果签名密钥丢失或改变，已安装应用将无法覆盖升级。应用不保存管理员密码；当前服务器使用 HTTP，因此 Android 清单暂时允许明文连接，生产环境配置 HTTPS 后应同步关闭该选项。
