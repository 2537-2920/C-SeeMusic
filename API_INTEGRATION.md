# SeeMusic 前后端 API 对接方案

## 配置说明

### 1. 后端配置

#### MySQL 连接配置
编辑 `backend/appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=seemusic;Uid=root;Pwd=your_password;"
  },
  "Jwt": {
    "Secret": "your-super-secret-key-change-this-in-production-at-least-32-characters",
    "Issuer": "SeeMusic",
    "Audience": "SeeMusic",
    "ExpiresInMinutes": 120
  }
}
```

#### 启动后端

```bash
cd backend
dotnet restore
dotnet run
```

后端将在 `https://localhost:5001` 运行，Swagger 文档在 `https://localhost:5001/swagger`

### 2. 前端集成

#### Client 端配置

在 `client/App.xaml.cs` 中初始化 API 客户端：

```csharp
using SeeMusicApp.Services;

public partial class App : Application
{
    public static ApiClient ApiClient { get; set; }
    public static AuthService AuthService { get; set; }

    public App()
    {
        var apiBaseUrl = "https://localhost:5001";
        ApiClient = new ApiClient(apiBaseUrl);
        AuthService = new AuthService(ApiClient);
    }
}
```

## API 使用示例

### 注册新用户

```csharp
var authService = App.AuthService;
var authResponse = await authService.RegisterAsync(
    "user_001",
    "user@email.com",
    "Password123!"
);

if (authResponse != null)
{
    MessageBox.Show($"注册成功！用户ID: {authResponse.User.Id}");
}
```

### 登录

```csharp
var authService = App.AuthService;
var authResponse = await authService.LoginAsync("user_001", "Password123!");

if (authResponse != null)
{
    MessageBox.Show($"登录成功！{authResponse.User.DisplayName}");
    // 后续请求会自动携带 AccessToken
}
```

### 获取当前用户信息

```csharp
var apiClient = App.ApiClient;
var response = await apiClient.GetAsync<UserInfo>("/api/v1/users/me");

if (response.Code == 0)
{
    var user = response.Data;
    MessageBox.Show($"用户名: {user.Username}");
}
```

### 更新用户信息

```csharp
var apiClient = App.ApiClient;
var userInfo = new UserInfo
{
    DisplayName = "新的昵称",
    Bio = "个人简介"
};

var response = await apiClient.PutAsync<UserInfo>("/api/v1/users/me", userInfo);

if (response.Code == 0)
{
    MessageBox.Show("更新成功");
}
```

### 上传媒体文件

```csharp
var apiClient = App.ApiClient;
var openFileDialog = new Microsoft.Win32.OpenFileDialog
{
    Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav"
};

if (openFileDialog.ShowDialog() == true)
{
    using var fileStream = File.OpenRead(openFileDialog.FileName);
    using var content = new MultipartFormDataContent();
    content.Add(new StreamContent(fileStream), "file", Path.GetFileName(openFileDialog.FileName));
    content.Add(new StringContent("audio"), "type");

    var response = await apiClient.PostFormAsync<MediaUploadResponse>("/api/v1/media/upload", content);
    
    if (response.Code == 0)
    {
        MessageBox.Show($"上传成功！MediaId: {response.Data.MediaId}");
    }
}
```

### 刷新令牌

```csharp
var authService = App.AuthService;
var newAuthResponse = await authService.RefreshTokenAsync(refreshToken);

if (newAuthResponse != null)
{
    MessageBox.Show("令牌已刷新");
}
```

## Docker Compose 本地开发

### 启动所有服务

```bash
docker-compose up -d
```

这将启动：
- MySQL 8.0 (端口 3306)
- SeeMusic 后端 (端口 5001)

### 查看日志

```bash
docker-compose logs -f backend
```

### 停止所有服务

```bash
docker-compose down
```

## CI/CD 流程

GitHub Actions 配置在 `.github/workflows/backend.yml`，会自动运行：

1. **Build**: 编译后端项目
2. **Test**: 运行单元测试（需要创建 Tests 项目）
3. **Code Quality**: 检查代码风格
4. **Docker**: 构建并推送 Docker 镜像到 GitHub Container Registry

### 触发条件

- 推送到 `main` 或 `develop` 分支时
- PR 针对 `main` 或 `develop` 时
- 只有修改 `backend/**` 目录下的文件才会触发

## 特性说明

### JWT 令牌

- **AccessToken**: 用于认证，有效期 120 分钟
- **RefreshToken**: 用于刷新 AccessToken，有效期 7 天

所有需要认证的请求都在 HTTP 头中加入：
```
Authorization: Bearer <access_token>
```

### 密码加密

密码使用 SHA256 哈希存储，不存储明文密码。

### 错误响应

```json
{
  "code": 40001,
  "message": "invalid request",
  "errors": {
    "field_name": ["错误信息"]
  }
}
```

## 下一步

1. **创建测试项目** - 在 `backend.Tests` 中添加单元测试和集成测试
2. **前端完整集成** - 将 LoginWindow 和其他模块接入 AuthService
3. **生产部署** - 配置真实的 MySQL 数据库和 JWT 密钥
4. **API 文档** - 使用 Swagger/OpenAPI 生成完整的 API 文档
