# SeeMusic 后端框架

这是 SeeMusic 后端的基础代码框架，采用 ASP.NET Core Web API。

## 目录结构

- `backend/SeeMusic.Backend.csproj`：后端项目文件
- `backend/Program.cs`：应用启动入口
- `backend/Controllers/`：API 控制器
- `backend/Models/`：数据传输对象与响应模型
- `backend/Services/`：简单业务服务实现
- `backend/Data/`：演示用内存数据库
- `backend/Extensions/`：服务注册扩展

## 运行方式

```bash
cd backend
dotnet restore
dotnet run
```

然后访问 `https://localhost:5001/swagger` 查看 Swagger API 文档。
