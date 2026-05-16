# SeeMusic 当前数据库说明

这份文档现在分成两层：

- 当前后端真实运行的核心结构
- 为社区和个人中心保留的兼容扩展结构

当前后端的数据库真实来源是：

- 实体定义：[backend/Models/Entities.cs](/Users/kugua/see-music/backend/Models/Entities.cs:1)
- EF Core 映射：[backend/Data/SeeMusicDbContext.cs](/Users/kugua/see-music/backend/Data/SeeMusicDbContext.cs:1)
- 可直接建库 SQL：[database.mysql.sql](/Users/kugua/see-music/database.mysql.sql:1)

## 1. 先回答你的问题

### 1.1 `database.mysql.sql` 是不是把表和字段变少了

最开始我给出的版本是变少了的，因为它只保留了“当前评估页和识谱页真实在跑的核心结构”。

但考虑到你同伴在社区和个人中心这部分已经改过，我现在已经把 `database.mysql.sql` 调整成：

- 核心运行表继续保留
- 社区和个人中心相关的表与字段继续保留
- 对当前后端暂时不用的字段，尽量采用 `NULL` 或安全默认值，避免影响现有代码插入

### 1.2 这样会不会有影响

如果保留这些社区/个人中心表和字段，对当前已经实现的页面也没有负面影响。

原因是当前后端真正支持的核心功能主要只有这些：

- 登录与 refresh token
- 媒体上传与音频预处理
- 歌唱评估
- 智能识谱的钢琴双手谱 MVP

而社区与个人中心相关结构，当前后端虽然还没有全部接上，但保留它们不会破坏现有运行。

所以现在的原则变成：

- 当前运行结构，以 `database.mysql.sql` 为准
- 社区和个人中心需要的表结构，在数据库里继续保留
- 当前后端未使用到的兼容字段，不作为核心运行字段强依赖

### 1.3 为什么你在实际数据库里 `DESC scores` 会看到更多字段

如果你在本地数据库里看到 `Scores` 除了当前文档里的 CamelCase 字段外，还额外存在下面这类 snake_case 历史列：

- `source_media_id`
- `cover_media_id`
- `key_signature`
- `time_signature`
- `tempo`
- `source_type`
- `is_public`
- `price_cent`
- `download_count`
- `favorite_count`
- `comment_count`
- `share_count`
- `cover_url`
- `file_url`
- `artist_name`
- `arrangement_tag`
- `primary_category`
- `published_at`

这通常不表示当前文档写错了，而是表示你的数据库是“旧结构和新结构叠加后的结果”。

原因一般有 3 个：

1. `docker-compose.yml` 使用了持久卷 `mysql-data`，旧库不会因为重启容器自动清空。
2. 当前 `docker-compose.yml` 并没有把 [database.mysql.sql](/Users/kugua/see-music/database.mysql.sql:1) 挂载到 MySQL 的初始化目录，所以它不会在每次启动时自动重建表。
3. 当前后端启动时走的是 [backend/Program.cs](/Users/kugua/see-music/backend/Program.cs:68) 里的 `EnsureCreated()` 或 `Migrate()` 逻辑；这会确保当前模型需要的表存在，但不会自动删除旧列。

所以要区分两件事：

- 文档中的 `Scores` 字段：表示当前仓库认定的目标结构
- 你本地库里实际多出来的旧列：表示历史遗留兼容状态，不代表当前后端正在使用它们

当前仓库里的后端核心代码直接对应的是 [backend/Models/Entities.cs](/Users/kugua/see-music/backend/Models/Entities.cs:110) 和 [backend/Data/SeeMusicDbContext.cs](/Users/kugua/see-music/backend/Data/SeeMusicDbContext.cs:118) 这套字段，不依赖上面那批 snake_case 历史列。

## 2. 当前数据库技术设定

- 当前后端使用 `MySQL`
- ORM 使用 `EF Core`
- Provider 使用 `Pomelo.EntityFrameworkCore.MySql`
- 连接与注册位置在 [backend/Program.cs](/Users/kugua/see-music/backend/Program.cs:35)

说明：

- 旧版文档里提到的 `SQLite`，更适合早期原型阶段
- 但当前项目后端实际已经按 `MySQL` 在运行
- 因此本文档统一按 `MySQL` 口径描述

## 3. 当前数据库总览

### 3.1 当前后端核心运行表

当前后端实际直接使用这些表：

1. `Users`
2. `RefreshTokens`
3. `MediaFiles`
4. `Evaluations`
5. `EvaluationSegments`
6. `EvaluationSuggestions`
7. `EvaluationExports`
8. `Scores`
9. `ScoreTracks`
10. `ScoreNotes`
11. `TranscriptionJobs`

### 3.2 为社区与个人中心保留的兼容扩展表

这些表已经重新纳入数据库文件中，方便你同伴那部分继续运行或继续开发：

1. `UserProfiles`
2. `UserPreferences`
3. `ScoreCategories`
4. `ScoreCategoryRelations`
5. `ScoreComments`
6. `ScoreFavorites`
7. `ScoreDownloads`
8. `ScoreOrders`
9. `ScoreExports`

此外，下面这些核心表也保留了社区/个人中心兼容字段：

- `Users`
- `MediaFiles`
- `Scores`
- `ScoreTracks`
- `ScoreNotes`

## 4. 当前表说明

### 4.1 `Users`

用途：

- 登录注册
- 用户基础资料

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `Username` | varchar(50) UNIQUE | 用户名 |
| `Email` | varchar(100) UNIQUE | 邮箱 |
| `PasswordHash` | longtext | 密码哈希 |
| `PasswordSalt` | longtext NULL | 兼容旧密码方案 |
| `DisplayName` | varchar(100) | 显示名 |
| `AvatarUrl` | longtext NULL | 头像地址 |
| `Bio` | varchar(500) | 个人简介 |
| `Status` | varchar(20) | 用户状态，默认 `active` |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) NULL | 更新时间 |
| `LastLoginAt` | datetime(6) | 最后登录时间 |

说明：

- 当前核心后端直接把展示资料放在 `Users`
- 同时数据库里也保留了兼容用的 `UserProfiles`

### 4.2 `RefreshTokens`

用途：

- 登录续期
- 多次登录后的 refresh token 存储

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `UserId` | int FK | 用户 ID |
| `Token` | longtext | refresh token |
| `ExpiresAt` | datetime(6) | 过期时间 |
| `RevokedAt` | datetime(6) NULL | 撤销时间 |
| `CreatedAt` | datetime(6) | 创建时间 |

### 4.3 `MediaFiles`

用途：

- 上传的音频文件
- 评估输入音频
- 识谱输入音频
- 导出文件记录依赖的媒体表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `MediaId` | varchar(50) UNIQUE | 对外公开 ID |
| `UserId` | int NULL FK | 上传用户 |
| `Bucket` | varchar(100) NULL | 兼容对象存储桶概念 |
| `FileName` | varchar(255) | 原文件名 |
| `Type` | varchar(20) | 业务类型，如 `audio` |
| `MimeType` | varchar(100) | MIME 类型 |
| `FileSize` | bigint | 文件大小 |
| `Url` | longtext | 对外访问路径 |
| `StoragePath` | varchar(255) | 存储路径 |
| `DurationMs` | int NULL | 时长 |
| `Width` | int NULL | 图片/视频宽 |
| `Height` | int NULL | 图片/视频高 |
| `MediaType` | varchar(20) NULL | 扩展媒体类型 |
| `PreparedAudioStatus` | varchar(20) | 预处理状态 |
| `PreparedAudioPath` | varchar(255) NULL | 标准化音频路径 |
| `PreparationErrorMessage` | varchar(500) NULL | 预处理失败信息 |
| `CreatedAt` | datetime(6) | 创建时间 |

说明：

- 这张表就是旧设计稿里 `media_assets` 的现实版本
- 名字现在是 `MediaFiles`
- 这次页面改版后，它比旧稿多了音频预处理相关字段

### 4.4 `Evaluations`

用途：

- 歌唱评估主任务表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `EvaluationId` | varchar(50) UNIQUE | 对外评估 ID |
| `UserId` | int NULL FK | 发起用户 |
| `PerformanceMediaFileId` | int FK | 用户演唱音频 |
| `ReferenceMediaFileId` | int NULL FK | 参考音频 |
| `Status` | varchar(20) | `queued / processing / succeeded / failed` |
| `Progress` | int | 0-100 |
| `AnalyzePitch` | tinyint(1) | 是否分析音准 |
| `AnalyzeRhythm` | tinyint(1) | 是否分析节奏 |
| `ScoringProfile` | varchar(40) | 评分模式结果 |
| `PitchStatus` | varchar(20) | 音准状态 |
| `RhythmStatus` | varchar(20) | 节奏状态 |
| `TotalScore` | double NULL | 综合分 |
| `PitchScore` | double NULL | 音准分 |
| `RhythmScore` | double NULL | 节奏分 |
| `DetectedTempoBpm` | double NULL | 检测到的速度 |
| `MeanPitchDeviationCents` | double NULL | 平均音高偏差 |
| `Badge` | varchar(30) | 评级标记 |
| `SummaryText` | varchar(1000) | 摘要文案 |
| `OptionsJson` | longtext | 请求参数 JSON |
| `WarningMessagesJson` | longtext | 警告 JSON |
| `PitchAnalysisJson` | longtext | 音准分析 JSON |
| `RhythmAnalysisJson` | longtext | 节奏分析 JSON |
| `TransposeBaseJson` | longtext | 变调建议基础信息 JSON |
| `ErrorMessage` | varchar(1000) | 失败信息 |
| `AnonymousTokenHash` | varchar(120) NULL | 匿名查询令牌哈希 |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) | 更新时间 |
| `StartedAt` | datetime(6) NULL | 开始时间 |
| `FinishedAt` | datetime(6) NULL | 完成时间 |

说明：

- 这比旧版设计稿更贴近当前歌唱评估页真实需求
- 变调建议现在不单独落表，而是通过 `TransposeBaseJson` 和接口即时生成/返回

### 4.5 `EvaluationSegments`

用途：

- 存储音准分段
- 存储节奏分段

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `EvaluationDbId` | int FK | 所属评估 |
| `MetricType` | varchar(20) | `pitch / rhythm` |
| `StartMs` | int | 起始时间 |
| `EndMs` | int | 结束时间 |
| `Score` | double NULL | 分段得分 |
| `DeviationValue` | double NULL | 偏差值 |
| `DeviationUnit` | varchar(20) NULL | `cents / ms` |
| `Severity` | varchar(20) | 严重级别 |
| `NoteText` | varchar(500) | 说明文本 |
| `SortOrder` | int | 排序 |

### 4.6 `EvaluationSuggestions`

用途：

- 存储评估建议

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `EvaluationDbId` | int FK | 所属评估 |
| `SuggestionType` | varchar(30) | 建议类型 |
| `Title` | varchar(120) | 标题 |
| `Content` | varchar(1000) | 建议内容 |
| `SortOrder` | int | 排序 |

### 4.7 `EvaluationExports`

用途：

- 记录评估 PDF 导出

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `EvaluationDbId` | int FK | 评估 ID |
| `MediaFileId` | int FK | 导出文件媒体 ID |
| `ExportType` | varchar(20) | 当前主要是 `pdf` |
| `CreatedByUserId` | int FK | 操作者 |
| `CreatedAt` | datetime(6) | 创建时间 |

### 4.8 `Scores`

用途：

- 智能识谱后的乐谱主表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `ScoreId` | varchar(50) UNIQUE | 对外乐谱 ID |
| `UserId` | int NULL FK | 所属用户 |
| `OwnerUserId` | int NULL FK | 兼容社区作者字段 |
| `SourceMediaFileId` | int FK | 来源音频 |
| `CoverMediaFileId` | int NULL FK | 封面媒体 |
| `Title` | varchar(200) | 乐谱标题 |
| `ArtistName` | varchar(200) NULL | 原曲作者/歌手 |
| `ArrangementTag` | varchar(100) NULL | 改编标签 |
| `Description` | longtext NULL | 描述 |
| `InstrumentMode` | varchar(30) | 当前固定 `piano` |
| `Status` | varchar(20) | 状态 |
| `SourceType` | varchar(20) NULL | 来源类型 |
| `IsPublic` | tinyint(1) | 是否公开 |
| `PriceCent` | int | 价格，默认 0 |
| `DownloadCount` | int | 下载次数 |
| `FavoriteCount` | int | 收藏次数 |
| `CommentCount` | int | 评论次数 |
| `TempoBpm` | double NULL | 速度 |
| `TimeSignature` | varchar(20) | 拍号 |
| `KeySignature` | varchar(20) | 调号 |
| `MeasureCount` | int | 小节数 |
| `EstimatedPageCount` | int | 估算页数 |
| `MusicXmlContent` | longtext | MusicXML 内容 |
| `AnalysisSummaryJson` | longtext | 分析摘要 JSON |
| `WarningMessagesJson` | longtext | 警告 JSON |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) | 更新时间 |
| `PublishedAt` | datetime(6) NULL | 发布时间 |

说明：

- 当前核心用途仍然是“钢琴双手谱 MVP”
- 但社区发布相关字段已经重新保留在这张表里

### 4.9 `ScoreTracks`

用途：

- 存储乐谱的轨道信息
- 当前至少是右手旋律、左手伴奏两条轨

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `ScoreDbId` | int FK | 所属乐谱 |
| `Name` | varchar(80) | 轨道名 |
| `HandRole` | varchar(20) | 左手/右手 |
| `Instrument` | varchar(40) | 当前为钢琴 |
| `ChannelNo` | int NULL | MIDI 通道号 |
| `NoteCount` | int | 音符数量 |
| `RangeLowMidi` | int NULL | 最低音 |
| `RangeHighMidi` | int NULL | 最高音 |
| `IsMuted` | tinyint(1) | 是否静音，默认 `0` |
| `IsVisible` | tinyint(1) | 是否显示，默认 `1` |
| `IsGenerated` | tinyint(1) | 是否规则生成 |
| `SummaryText` | varchar(500) | 摘要 |
| `SortOrder` | int | 排序 |
| `CreatedAt` | datetime(6) | 创建时间 |

### 4.10 `ScoreNotes`

用途：

- 存储规范化后的音符事件
- 当前主要用于渲染和内部分析

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `ScoreDbId` | int FK | 所属乐谱 |
| `ScoreTrackDbId` | int FK | 所属轨道 |
| `MeasureNo` | int | 小节号 |
| `BeatStart` | double | 起拍位置 |
| `DurationType` | varchar(20) | 时值类型 |
| `DurationBeats` | double | 实际拍长 |
| `DurationValue` | double NULL | 原始时值数值 |
| `PitchName` | varchar(10) | 音名，如 `C4` |
| `MidiNumber` | int | MIDI 音高 |
| `Velocity` | int | 力度，默认 `64` |
| `Staff` | varchar(20) | 谱表归属 |
| `StartTimeSeconds` | double | 原始时间轴起点 |
| `IsChordTone` | tinyint(1) | 是否和弦音 |
| `StaffX` | double NULL | 版面横坐标 |
| `StaffY` | double NULL | 版面纵坐标 |
| `SortOrder` | int | 排序 |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) | 更新时间 |

说明：

- 虽然当前前端不支持音符编辑
- 但这张表仍然保留，因为识谱结果持久化和后续扩展都要用

### 4.11 `TranscriptionJobs`

用途：

- 智能识谱任务表
- 支撑同步优先、异步兜底的处理模式

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `JobId` | varchar(50) UNIQUE | 对外任务 ID |
| `UserId` | int NULL FK | 发起用户 |
| `SourceMediaFileId` | int FK | 输入音频 |
| `ScoreDbId` | int NULL FK | 结果乐谱 |
| `ProjectTitle` | varchar(200) | 项目标题 |
| `SourceType` | varchar(20) | 当前主要是 `audio` |
| `Status` | varchar(20) | `queued / processing / succeeded / failed` |
| `Progress` | int | 进度 |
| `OptionsJson` | longtext | 识谱参数 JSON |
| `ErrorMessage` | varchar(1000) | 错误信息 |
| `DetectedTempoBpm` | double NULL | 识别到的 BPM |
| `DetectedTimeSignature` | varchar(20) NULL | 识别到的拍号 |
| `MeasureCount` | int NULL | 小节数 |
| `EstimatedPageCount` | int NULL | 估算页数 |
| `BeatAnalysisJson` | longtext | 节拍分析 JSON |
| `WarningMessagesJson` | longtext | 警告 JSON |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) | 更新时间 |
| `StartedAt` | datetime(6) NULL | 开始时间 |
| `FinishedAt` | datetime(6) NULL | 完成时间 |

### 4.12 `UserProfiles`

用途：

- 个人中心扩展资料
- 社区展示资料兼容表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `UserId` | int PK / FK | 对应 `Users.Id` |
| `DisplayName` | varchar(100) NULL | 扩展显示名 |
| `AvatarMediaFileId` | int NULL FK | 头像媒体文件 |
| `Bio` | longtext NULL | 扩展简介 |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) | 更新时间 |

说明：

- 当前核心后端把基础资料直接放在 `Users`
- 这张表主要用于兼容个人中心和社区扩展资料设计

### 4.13 `UserPreferences`

用途：

- 用户偏好设置
- 导出与同步习惯配置

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `UserId` | int PK / FK | 对应 `Users.Id` |
| `Theme` | varchar(50) | 主题，默认 `default` |
| `DefaultExportFormats` | longtext NULL | 默认导出格式配置 |
| `SyncEnabled` | tinyint(1) | 是否启用同步，默认 `1` |
| `UpdatedAt` | datetime(6) | 更新时间 |

### 4.14 `ScoreCategories`

用途：

- 乐谱分类字典表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `Name` | varchar(100) UNIQUE | 分类名 |
| `Slug` | varchar(100) UNIQUE | 分类标识 |
| `SortOrder` | int | 排序，默认 `0` |

### 4.15 `ScoreCategoryRelations`

用途：

- 乐谱与分类的多对多关系表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `ScoreDbId` | int PK / FK | 乐谱 ID |
| `CategoryId` | int PK / FK | 分类 ID |

### 4.16 `ScoreComments`

用途：

- 乐谱评论

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `ScoreDbId` | int FK | 所属乐谱 |
| `UserId` | int FK | 评论用户 |
| `Content` | longtext | 评论内容 |
| `CreatedAt` | datetime(6) | 创建时间 |
| `UpdatedAt` | datetime(6) NULL | 更新时间 |
| `Status` | varchar(20) | 状态，默认 `visible` |

### 4.17 `ScoreFavorites`

用途：

- 乐谱收藏关系表

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `UserId` | int PK / FK | 收藏用户 |
| `ScoreDbId` | int PK / FK | 被收藏乐谱 |
| `CreatedAt` | datetime(6) | 创建时间 |

### 4.18 `ScoreDownloads`

用途：

- 乐谱下载记录

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `ScoreDbId` | int FK | 乐谱 ID |
| `UserId` | int NULL FK | 下载用户，允许匿名 |
| `SourceIp` | varchar(64) NULL | 来源 IP |
| `CreatedAt` | datetime(6) | 创建时间 |

### 4.19 `ScoreOrders`

用途：

- 乐谱购买订单

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `UserId` | int FK | 下单用户 |
| `ScoreDbId` | int FK | 乐谱 ID |
| `AmountCent` | int | 订单金额，默认 `0` |
| `Status` | varchar(20) | 订单状态，默认 `pending` |
| `CreatedAt` | datetime(6) | 创建时间 |
| `PaidAt` | datetime(6) NULL | 支付时间 |

### 4.20 `ScoreExports`

用途：

- 乐谱导出文件记录

当前字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | int PK | 主键 |
| `ScoreDbId` | int FK | 乐谱 ID |
| `MediaFileId` | int FK | 导出文件媒体 ID |
| `ExportType` | varchar(20) | 导出类型 |
| `CreatedByUserId` | int FK | 操作者 |
| `CreatedAt` | datetime(6) | 创建时间 |

## 5. 当前表关系

当前 SQL 中的主要关系如下：

- `Users 1 -> n RefreshTokens`
- `Users 1 -> n MediaFiles`
- `Users 1 -> n Evaluations`
- `Users 1 -> n Scores`，通过 `Scores.UserId`
- `Users 1 -> n Scores`，通过 `Scores.OwnerUserId`
- `Users 1 -> n TranscriptionJobs`
- `Users 1 -> 1 UserProfiles`
- `Users 1 -> 1 UserPreferences`
- `Users 1 -> n ScoreComments`
- `Users 1 -> n ScoreDownloads`
- `Users 1 -> n ScoreOrders`
- `Users 1 -> n ScoreExports`
- `Users n -> n Scores`，通过 `ScoreFavorites`
- `MediaFiles 1 -> n Evaluations`，通过演唱音频与参考音频外键
- `MediaFiles 1 -> n Scores`，通过来源音频与封面媒体外键
- `MediaFiles 1 -> n TranscriptionJobs`
- `MediaFiles 1 -> n EvaluationExports`
- `MediaFiles 1 -> n ScoreExports`
- `Evaluations 1 -> n EvaluationSegments`
- `Evaluations 1 -> n EvaluationSuggestions`
- `Evaluations 1 -> n EvaluationExports`
- `Scores 1 -> n ScoreTracks`
- `Scores 1 -> n ScoreNotes`
- `Scores 1 -> n TranscriptionJobs`
- `Scores 1 -> n ScoreCategoryRelations`
- `Scores 1 -> n ScoreComments`
- `Scores 1 -> n ScoreDownloads`
- `Scores 1 -> n ScoreOrders`
- `Scores 1 -> n ScoreExports`
- `ScoreTracks 1 -> n ScoreNotes`
- `ScoreCategories 1 -> n ScoreCategoryRelations`

## 6. 当前页面与表的对应关系

### 6.1 歌唱评估页

使用这些表：

- `MediaFiles`
- `Evaluations`
- `EvaluationSegments`
- `EvaluationSuggestions`
- `EvaluationExports`

说明：

- 当前“变调建议”不单独落表
- 当前也没有 `variation_suggestions`

### 6.2 智能识谱页

使用这些表：

- `MediaFiles`
- `TranscriptionJobs`
- `Scores`
- `ScoreTracks`
- `ScoreNotes`

说明：

- 当前只做钢琴双手谱
- 当前不做音符编辑接口，但 `ScoreNotes` 已经落库
- 当前 PDF 导出走前端本地打印，不走后端 `ScoreExports`

### 6.3 社区页

数据库已经保留这些表和字段：

- `Scores` 里的公开、价格、统计、封面、作者相关字段
- `ScoreCategories`
- `ScoreCategoryRelations`
- `ScoreComments`
- `ScoreFavorites`
- `ScoreDownloads`
- `ScoreOrders`
- `ScoreExports`

说明：

- 当前仓库里的社区接口文档仍然成立
- 即使当前核心后端还没把社区接口全部接完，数据库层先保留兼容结构是安全的

### 6.4 个人中心

数据库已经保留这些结构：

- `Users`
- `UserProfiles`
- `UserPreferences`
- `ScoreFavorites`
- `TranscriptionJobs`
- `Evaluations`

说明：

- 当前个人资料既可以从 `Users` 直接取
- 也可以为你同伴保留 `UserProfiles / UserPreferences` 这套扩展表

### 6.5 登录相关

使用这些表：

- `Users`
- `RefreshTokens`

## 7. 当前兼容保留的扩展结构

这些结构现在已经重新保留在数据库 SQL 里，用来兼容社区和个人中心：

### 7.1 已保留的扩展表

- `UserProfiles`
- `UserPreferences`
- `ScoreCategories`
- `ScoreCategoryRelations`
- `ScoreComments`
- `ScoreFavorites`
- `ScoreDownloads`
- `ScoreOrders`
- `ScoreExports`

### 7.2 已保留的兼容字段

主要包括：

- `Users.Status`
- `Users.UpdatedAt`
- `Users.PasswordSalt`
- `MediaFiles.Bucket`
- `MediaFiles.Width`
- `MediaFiles.Height`
- `MediaFiles.MediaType`
- `Scores.OwnerUserId`
- `Scores.CoverMediaFileId`
- `Scores.ArtistName`
- `Scores.ArrangementTag`
- `Scores.Description`
- `Scores.SourceType`
- `Scores.IsPublic`
- `Scores.PriceCent`
- `Scores.DownloadCount`
- `Scores.FavoriteCount`
- `Scores.CommentCount`
- `Scores.PublishedAt`
- `ScoreTracks.ChannelNo`
- `ScoreTracks.IsMuted`
- `ScoreTracks.IsVisible`
- `ScoreNotes.DurationValue`
- `ScoreNotes.Velocity`
- `ScoreNotes.StaffX`
- `ScoreNotes.StaffY`

### 7.3 这样保留的意义

- 不会影响当前歌唱评估和识谱功能
- 可以避免你同伴那部分因为表或字段缺失而无法运行
- 让数据库文件同时覆盖“当前可运行功能”和“已规划并已有人开发的功能”

## 8. 后续如果要扩展，建议怎么做

如果以后你要继续把社区和个人中心真正接进后端服务，建议做的是“补实体和接口”，而不是再改掉这些表：

### 8.1 社区页

补这些表：

- `ScoreCategories`
- `ScoreCategoryRelations`
- `ScoreComments`
- `ScoreFavorites`
- `ScoreDownloads`
- `ScoreOrders`
- `ScoreExports`

### 8.2 云端乐谱编辑

当前 `ScoreNotes` 已经可以作为基础。

如果以后真做可编辑乐谱，建议再补：

- `ScoreRevisions`
- `ScoreEditLogs`

### 8.3 用户资料扩展

当前这两张表已经保留：

- `UserProfiles`
- `UserPreferences`

下一步更适合做的是：

- 让后端正式读写这两张表
- 或者明确改成只用 `Users` 表并做一次统一迁移

## 9. 现在应该以哪个文件为准

优先级建议如下：

1. 真正建库：以 [database.mysql.sql](/Users/kugua/see-music/database.mysql.sql:1) 为准
2. 理解核心运行表与兼容扩展表：看本文档
3. 理解当前后端真正直接读写的核心结构：看 [backend/Models/Entities.cs](/Users/kugua/see-music/backend/Models/Entities.cs:1) 和 [backend/Data/SeeMusicDbContext.cs](/Users/kugua/see-music/backend/Data/SeeMusicDbContext.cs:1)

补充说明：

- 如果你查看的是一个已经跑过旧版本的本地 MySQL 实例，那么实际表结构可能比本文档更多，因为旧列不会被自动删掉。
- 如果你希望本地库和本文档完全一致，最稳妥的做法不是继续补文档，而是先备份数据，再按 [database.mysql.sql](/Users/kugua/see-music/database.mysql.sql:1) 重新建库。

如果你愿意，我下一步可以继续帮你做两件事中的一个：

1. 把旧数据库设计稿继续细化成“未来完整版规划”
2. 直接给你一份“从旧库升级到当前库”的 `ALTER TABLE` 迁移 SQL
