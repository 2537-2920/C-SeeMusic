SeeMusic —— 智能音频扒谱与评估工具
SeeMusic 是一款基于 AI 技术的智能音乐辅助工具。它能够将音频/视频中的旋律自动转换为五线谱，并提供完整的乐谱编辑、智能音准节奏评估以及多格式导出功能，旨在为音乐学习者和创作者提供从“听到”到“写出”的无缝体验。
🚀 核心特性
🎹 AI 智能扒谱：支持本地音频、麦克风录音及视频提取，利用 AI 算法实现高精度的音高识别与实时音准检测。
🎼 完整乐谱编辑器：自动生成五线谱，支持音符增删改、节拍编辑、升降调及撤销/重做操作。
📊 智能评估报告：针对演奏进行音准打分、节奏分析及错误高亮，并导出详细的学习评估报告。
📁 多格式导出：支持将生成的乐谱导出为图片、PDF 或 MIDI 文件，方便二次创作或分享。
👥 用户生态系统：内置用户注册登录系统，支持历史记录隔离管理及乐谱分享社区。
🛠️ 技术栈 
模块    技术实现    理由
客户端 (Frontend)    C# Winform (.NET 6/8)    利用 GDI+ 实现高性能五线谱渲染，NAudio 进行底层音频流捕获
后端 (Backend)    ASP.NET Core Web API    极高的吞吐量，支持 RESTful 接口，方便进行模型类共享
算法实现 (Algorithms)    Math.Net Numerics + NAudio    纯 C# 实现 FFT 变换与 DTW 节奏比对逻辑，无需 Python 运行时
AI 模型推理    ONNX Runtime (.NET)    加载预训练的深度学习模型，实现跨平台的 AI 音高识别
数据库 (Database)    SQLite + EF Core    轻量化本地存储，使用实体框架进行对象关系映射
多媒体处理    FFmpeg.AutoGen    用于从录制的视频文件中高速提取 PCM 音频流
📂项目结构
SeeMusic_Solution/
├── SeeMusic.Shared/              # 【共享库】前后端通用，定义统一的数据契约
│   ├── Models/                   # 核心数据模型 (对应 MySQL 表结构)
│   │   ├── User.cs               # 用户实体 (ID, 账号, 加密密码)
│   │   ├── MusicScore.cs         # 乐谱元数据 (标题, 调性, 速度)
│   │   └── Note.cs               # 音符实体 (音高, 时值, 偏移量)
│   ├── DTOs/                     # 数据传输对象 (用于 API 接口)
│   │   ├── AuthRequest.cs        # 登录/注册请求包
│   │   └── AnalysisResultDto.cs  # 后端返回的算法识别结果
│   ├── Enums/                    # 全局枚举 (InstrumentType, ClefType)
│   └── Constants/                # 全局常量 (API路由, 错误码定义)
│
├── SeeMusic.Server/              # 【后端】ASP.NET Core 8.0 Web API
│   ├── Controllers/              # RESTful API 控制器
│   │   ├── AuthController.cs     # 鉴权中心
│   │   ├── AudioController.cs    # 音视频上传及 FFmpeg 转码处理
│   │   └── ScoreController.cs    # 乐谱保存、查询及社区分享逻辑
│   ├── Core/                     # 核心算法引擎层
│   │   ├── AI/                   # ONNX Runtime 推理 (加载 .onnx 模型)
│   │   ├── DSP/                  # 数字信号处理 (FFT 变换, 采样率转换)
│   │   └── Scoring/              # 评分算法 (C# 实现的 DTW 比对逻辑)
│   ├── Services/                 # 核心业务逻辑
│   │   ├── MediaService.cs       # 视频提取音频、多格式转码服务
│   │   └── TranscriptionService.cs # 协调 AI 与 DSP 进行扒谱任务
│   ├── Data/                     # 数据库访问层 (EF Core)
│   │   ├── SeeMusicDbContext.cs  # MySQL 数据库上下文配置
│   │   └── Migrations/           # MySQL 迁移历史记录
│   ├── Storage/                  # 物理文件存储 (原始音频, 生成的PDF/MIDI)
│   └── appsettings.json          # 后端配置 (MySQL 连接字符串, AI 阈值)
│
├── SeeMusic.Client/              # 【前端】WPF 桌面客户端 (MVVM)
│   ├── Views/                    # XAML 界面文件
│   │   ├── ShellView.xaml        # 主框架窗口 (Navigation Drawer)
│   │   ├── RecorderView.xaml     # 录音与音频输入界面
│   │   └── EditorView.xaml       # 核心五线谱编辑器界面
│   ├── ViewModels/               # 业务绑定逻辑 (CommunityToolkit.Mvvm)
│   │   ├── MainViewModel.cs      # 导航控制逻辑
│   │   └── EditorViewModel.cs    # 音符编辑、撤销重做、实时打分逻辑
│   ├── Controls/                 # WPF 自定义渲染组件
│   │   ├── MusicStaffCanvas.cs   # 五线谱高性能矢量渲染引擎 (DrawingContext)
│   │   └── WaveformControl.cs    # 实时音频波形显示器
│   ├── Services/                 # 客户端特有服务
│   │   ├── AudioCaptureService.cs# 基于 NAudio 的录音服务
│   │   └── ApiClientService.cs   # 封装 HttpClient 调用后端接口
│   ├── Resources/                # UI 资源 (样式表, 图标, 字体)
│   └── App.xaml                  # 客户端入口及全局资源定义
│
├── SeeMusic.ML/                  # 【AI 资产库】
│   ├── Models/                   # 预训练模型文件 (pitch.onnx, beat_detect.onnx)
│   └── Inference/                # 模型初始化与跨平台推理辅助类
│
└── SeeMusic.Tests/               # 【测试工程】
    ├── UnitTests/                # 针对 DTW 和音高识别算法的数学验证
    └── IntegrationTests/         # 前后端 API 联调与 MySQL 写入测试    
📝 项目开发阶段划分 (Roadmap)
第一阶段：基础设施 (已确定)
配置 MySQL 数据库与 EF Core 映射
完成 Shared 库中乐谱数据结构（Note, Score）的设计
搭建 WPF 主框架并封装后端的 HttpClient 异步请求
第二阶段：算法迁移 (重点)
在后端集成 ONNX Runtime，将原 Python 逻辑迁移至 C#
编写 DTW 评分算法的 C# 实现
集成 FFmpeg 解决视频输入问题
第三阶段：WPF 渲染引擎
攻克 MusicStaffCanvas：实现由数据驱动的高性能五线谱绘制
实现音符的鼠标交互（点击、拖拽）
第四阶段：评估与导出
联调智能评估接口，在前端展示可视化纠错报告
实现 PDF 与 MIDI 的一键导出
🚀 快速开始 (Quick Start)
1. 环境准备
安装 Visual Studio 2022 (含 .NET 桌面开发与 ASP.NET 模块)
安装 SQLite 浏览器用于查看本地数据
2. 启动后端
powershell
cd SeeMusic.Server
dotnet run
# API 文档地址：https://localhost:5001/swagger
3. 运行客户端
在 Visual Studio 中设置 SeeMusic.Client 为启动项
修改 App.config 中的 ApiBaseUrl 为后端运行地址
按 F5 编译并运行
🧪 测试体系 (Testing)
单元测试 (Unit Tests)：使用 xUnit 针对 Shared 库中的节拍转换逻辑、音符偏移计算进行高覆盖率测试
集成测试 (Integration Tests)：模拟 Winform 发送 MultipartFormData (音频文件) 到 Web API，验证后端解析与存储的完整性
算法验证：对比 C# 版 DTW 与 Python 版 fastdtw 的输出结果，确保毫秒级比对的误差在 ±5ms 以内
并发测试：使用 JMeter 测试同时有多个扒谱请求时，后端的任务调度与 CPU 占用情况
💡 未来展望 (Future Outlook)
全平台覆盖：未来计划将 UI 层迁移至 MAUI，从而实现一套代码同时运行在 Windows、Android 和 iOS 上
实时协作：引入 SignalR 技术，实现乐谱社区的实时协同编辑功能
VST 插件化：将核心扒谱算法封装为 VST 插件，直接嵌入宿主软件 (DAW) 如 FL Studio 或 Cubase 中使用
生成式 AI：利用 LLM 根据用户历史偏好，自动生成练习建议或进行乐曲变奏重写
