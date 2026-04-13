# SeeMusic

SeeMusic 当前是一个桌面客户端 + Web API 后端的基础代码框架，适合作为后续功能开发的起点。

## 当前结构

- `backend/`: ASP.NET Core 8 Web API，使用 EF Core、Pomelo MySQL、JWT 鉴权和 Swagger
- `backend.Tests/`: xUnit 测试项目，当前包含 JWT 相关基础单元测试
- `client/`: WPF 桌面客户端，项目目标框架为 `.NET Framework 4.8`
- `docker-compose.yml`: 本地联调用的 MySQL + backend 容器编排
- `api.md`、`database.md`、`API_INTEGRATION.md`: 接口、数据库和联调说明文档

## 当前状态

- 后端基础框架可编译，并可运行测试
- 客户端是 Windows 专用的 WPF 工程，不支持在 macOS/Linux 上直接构建
- 仓库目前是基础框架，不包含 README 旧版本中描述的完整 AI 扒谱、导出、社区等成品能力

## 环境要求

### 后端

- `.NET 8 SDK`
- `MySQL 8`

### 客户端

- Windows
- Visual Studio 2022
- `.NET Framework 4.8 Developer Pack`

## 后端配置

启动前需要先配置后端数据库和 JWT：

- `backend/appsettings.json`
- 或使用环境变量覆盖 `ConnectionStrings__DefaultConnection` 和 `Jwt__Secret`

最少需要配置：

- `ConnectionStrings:DefaultConnection`
- `Jwt:Secret`

`Jwt:Secret` 至少需要 32 个字符。

## 本地运行

### 启动后端

```bash
cd backend
dotnet restore
dotnet run
```

开发环境下可通过 `https://localhost:5001/swagger` 查看接口文档。

### 运行后端测试

```bash
dotnet test backend.Tests/SeeMusic.Backend.Tests.csproj
```

### 运行客户端

在 Windows 上用 Visual Studio 打开 `client/SeeMusicApp.sln`，然后直接编译运行。

## GitHub Actions

当前工作流会在 `backend/` 或 `backend.Tests/` 变更时执行：

- 后端 restore / build
- `backend.Tests` 测试
- 后端 Docker 镜像构建

## 说明

- 客户端和后端目前还没有共享解决方案文件，仓库按目录分开维护
- 后端默认依赖 MySQL，启动时会执行数据库迁移
- 如果你准备公开仓库，建议继续把真实开发配置放到本地环境变量中，不要提交个人数据库账号或私钥
