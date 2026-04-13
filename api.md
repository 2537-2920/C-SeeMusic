# SeeMusic API 设计稿

本文档基于当前前端原型代码与 `README.md` 生成，目标是把现有界面需求整理成一套可实现的后端接口规范。

说明：

- 当前 `client/` 目录中的前端代码主要是界面原型，尚未接入真实 HTTP 请求。
- 因此本文档中的接口属于“推荐实现方案”，用于指导后端落地。
- 接口风格默认采用 `ASP.NET Core Web API + JSON + RESTful`。
- 文件上传接口采用 `multipart/form-data`。
- 鉴权建议采用 `JWT Access Token + Refresh Token`。

## 1. 基础约定

### 1.1 Base URL

```txt
/api/v1
```

### 1.2 通用响应格式

成功：

```json
{
  "code": 0,
  "message": "ok",
  "data": {}
}
```

失败：

```json
{
  "code": 40001,
  "message": "invalid request",
  "errors": {
    "username": ["用户名不能为空"]
  }
}
```

### 1.3 鉴权头

```http
Authorization: Bearer <access_token>
```

### 1.4 状态枚举建议

- `score_status`: `draft`, `processing`, `ready`, `published`, `archived`
- `job_status`: `queued`, `processing`, `succeeded`, `failed`
- `source_type`: `audio`, `video`, `microphone`, `sample`
- `export_type`: `pdf`, `midi`, `png`, `musicxml`

## 2. 鉴权与用户

对应界面：

- [client/LoginWindow.xaml](/Users/kugua/see-music/client/LoginWindow.xaml)
- [client/ProfileWindow.xaml](/Users/kugua/see-music/client/ProfileWindow.xaml)

### 2.1 注册

`POST /auth/register`

请求：

```json
{
  "username": "creator01",
  "email": "creator@seemusic.art",
  "password": "StrongPassword123",
  "confirmPassword": "StrongPassword123"
}
```

返回：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "userId": 1,
    "username": "creator01"
  }
}
```

### 2.2 登录

`POST /auth/login`

请求：

```json
{
  "account": "creator01",
  "password": "StrongPassword123"
}
```

返回：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "accessToken": "jwt-access-token",
    "refreshToken": "jwt-refresh-token",
    "expiresIn": 7200,
    "user": {
      "id": 1,
      "username": "creator01",
      "displayName": "灵感创作者",
      "avatarUrl": null
    }
  }
}
```

### 2.3 刷新令牌

`POST /auth/refresh`

### 2.4 登出

`POST /auth/logout`

### 2.5 获取当前用户资料

`GET /users/me`

返回字段建议：

- `id`
- `username`
- `displayName`
- `email`
- `avatarUrl`
- `bio`
- `createdAt`
- `lastLoginAt`

### 2.6 更新个人资料

`PUT /users/me`

请求：

```json
{
  "displayName": "灵感创作者",
  "bio": "专注扒谱、演唱分析与钢琴改编"
}
```

### 2.7 上传头像

`POST /users/me/avatar`

表单字段：

- `file`

## 3. 智能扒谱

对应界面：

- [client/TranscriptionWindow.xaml](/Users/kugua/see-music/client/TranscriptionWindow.xaml)
- [client/TranscriptionWindow.xaml.cs](/Users/kugua/see-music/client/TranscriptionWindow.xaml.cs)

界面已体现的需求：

- 支持本地音频
- 支持麦克风录音
- 支持视频提取音频
- 支持内置示例
- 支持旋律分离、伴奏分离、节奏分析
- 支持重新分析
- 支持编辑音符
- 支持导出 PDF/MIDI/PNG

### 3.1 上传原始媒体文件

`POST /media/upload`

表单字段：

- `file`
- `type`: `audio` 或 `video`

返回：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "mediaId": "med_001",
    "fileName": "demo.mp3",
    "mimeType": "audio/mpeg",
    "durationMs": 86321,
    "url": "/storage/media/demo.mp3"
  }
}
```

### 3.2 创建扒谱任务

`POST /transcriptions`

请求：

```json
{
  "sourceType": "audio",
  "mediaId": "med_001",
  "sampleId": null,
  "options": {
    "separateMelody": true,
    "separateAccompaniment": true,
    "analyzeRhythm": true,
    "styleHint": "pop"
  }
}
```

返回：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "jobId": "job_001",
    "status": "queued"
  }
}
```

### 3.3 查询扒谱任务状态

`GET /transcriptions/{jobId}`

返回字段建议：

- `jobId`
- `status`
- `progress`
- `sourceType`
- `errorMessage`
- `scoreId`
- `detectedTempo`
- `detectedTimeSignature`
- `tracks`

### 3.4 获取扒谱结果详情

`GET /scores/{scoreId}`

返回建议同时带出：

- 乐谱基础信息
- 主旋律轨道
- 音符列表
- 调号、拍号、BPM
- 原始媒体引用
- 最近一次算法分析摘要

### 3.5 重新分析乐谱

`POST /scores/{scoreId}/reanalyze`

请求体与 `POST /transcriptions` 基本一致，允许修改分析参数。

### 3.6 更新乐谱元信息

`PUT /scores/{scoreId}`

请求：

```json
{
  "title": "夜的钢琴曲",
  "tempo": 120,
  "timeSignature": "4/4",
  "keySignature": "C",
  "status": "draft"
}
```

### 3.7 新增音符

`POST /scores/{scoreId}/notes`

请求：

```json
{
  "trackId": "track_main",
  "measureNo": 1,
  "beatStart": 1.0,
  "duration": "quarter",
  "pitch": "C4",
  "midiNumber": 60,
  "velocity": 80,
  "staffX": 120.5,
  "staffY": 24.0
}
```

### 3.8 修改音符

`PUT /scores/{scoreId}/notes/{noteId}`

适用于：

- 时值修改
- 音高修改
- 力度修改
- 坐标调整

### 3.9 删除音符

`DELETE /scores/{scoreId}/notes/{noteId}`

### 3.10 获取音轨列表

`GET /scores/{scoreId}/tracks`

用于驱动左侧“音轨管理”区域。

### 3.11 导出乐谱

`POST /scores/{scoreId}/exports`

请求：

```json
{
  "format": "pdf"
}
```

返回：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "exportId": "exp_001",
    "format": "pdf",
    "downloadUrl": "/storage/exports/score_001.pdf"
  }
}
```

## 4. 歌唱评估

对应界面：

- [client/SingingEvaluationWindow.xaml](/Users/kugua/see-music/client/SingingEvaluationWindow.xaml)
- [client/SingingEvaluationWindow.xaml.cs](/Users/kugua/see-music/client/SingingEvaluationWindow.xaml.cs)

界面已体现的需求：

- 上传演唱作品
- 可选上传原唱或伴奏参考
- 评估音准、节奏、情感表达
- 输出综合评分和纠错建议
- 导出 PDF 报告
- 生成 AI 变奏建议

### 4.1 上传评估素材

`POST /evaluations/media`

表单字段：

- `performanceFile`
- `referenceFile` 可选

### 4.2 创建评估任务

`POST /evaluations`

请求：

```json
{
  "performanceMediaId": "med_perf_001",
  "referenceMediaId": "med_ref_001",
  "options": {
    "analyzePitch": true,
    "analyzeRhythm": true,
    "analyzeEmotion": true,
    "generateVariations": true
  }
}
```

### 4.3 查询评估任务状态

`GET /evaluations/{evaluationId}`

返回建议：

- `status`
- `totalScore`
- `pitchAccuracy`
- `rhythmStability`
- `emotionExpression`
- `segments`
- `suggestions`
- `variationSuggestions`

### 4.4 获取评估详情

`GET /evaluations/{evaluationId}/report`

响应建议：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "summary": {
      "totalScore": 88,
      "pitchAccuracy": 92,
      "rhythmStability": 85,
      "emotionExpression": 88
    },
    "pitchSegments": [
      {
        "startMs": 1200,
        "endMs": 1680,
        "deviationCents": 24,
        "severity": "warning"
      }
    ],
    "suggestions": [
      "副歌第一句存在半音偏高，建议加强气息支撑。"
    ],
    "variationSuggestions": [
      {
        "style": "jazz",
        "title": "爵士风格变奏",
        "description": "加入切分音与颤音处理，增强自由感。"
      }
    ]
  }
}
```

### 4.5 导出评估报告

`POST /evaluations/{evaluationId}/exports`

请求：

```json
{
  "format": "pdf"
}
```

## 5. 乐谱社区

对应界面：

- [client/CommunityWindow.xaml](/Users/kugua/see-music/client/CommunityWindow.xaml)
- [client/CommunityWindow.xaml.cs](/Users/kugua/see-music/client/CommunityWindow.xaml.cs)

界面已体现的需求：

- 搜索乐谱
- 分类筛选
- 乐谱列表卡片
- 乐谱详情
- 下载
- 收藏
- 评论
- 上传乐谱
- 免费/付费标识

### 5.1 社区乐谱列表

`GET /community/scores`

查询参数：

- `keyword`
- `category`
- `sort`: `featured`, `latest`, `popular`, `downloads`
- `page`
- `pageSize`

返回字段建议：

- `id`
- `title`
- `authorName`
- `arrangementTag`
- `coverUrl`
- `price`
- `isFree`
- `downloadCount`
- `favoriteCount`

### 5.2 获取乐谱详情

`GET /community/scores/{scoreId}`

需要包含：

- 基础信息
- 作者信息
- 价格信息
- 下载量
- 收藏量
- 评论列表
- 资源下载地址或下载凭证

### 5.3 上传社区乐谱

`POST /community/scores`

表单字段建议：

- `title`
- `artistName`
- `arrangementTag`
- `category`
- `price`
- `description`
- `coverFile` 可选
- `scoreFile` 必填，支持 `pdf`、`midi`、`musicxml`

### 5.4 评论列表

`GET /community/scores/{scoreId}/comments`

### 5.5 发表评论

`POST /community/scores/{scoreId}/comments`

请求：

```json
{
  "content": "这首谱子转调处理得非常巧妙，值得学习！"
}
```

### 5.6 收藏乐谱

`POST /community/scores/{scoreId}/favorite`

### 5.7 取消收藏

`DELETE /community/scores/{scoreId}/favorite`

### 5.8 下载乐谱

`POST /community/scores/{scoreId}/download`

建议行为：

- 记录下载日志
- 如果是付费资源，先校验购买权限
- 返回一次性下载地址或文件流

## 6. 个人中心

对应界面：

- [client/ProfileWindow.xaml](/Users/kugua/see-music/client/ProfileWindow.xaml)

界面已体现的需求：

- 展示用户统计数据
- 账号安全
- 系统偏好设置
- 默认导出格式
- 收藏数量
- 扒谱记录数量
- 评估时长

### 6.1 获取个人中心概览

`GET /users/me/dashboard`

返回建议：

```json
{
  "code": 0,
  "message": "ok",
  "data": {
    "profile": {
      "displayName": "灵感创作者",
      "email": "creator@seemusic.art",
      "avatarUrl": null
    },
    "stats": {
      "transcriptionCount": 42,
      "evaluationDurationHours": 15,
      "favoriteCount": 128
    },
    "weeklyUsage": [
      { "day": "Mon", "value": 60 },
      { "day": "Tue", "value": 95 },
      { "day": "Wed", "value": 140 }
    ]
  }
}
```

### 6.2 获取用户偏好

`GET /users/me/preferences`

### 6.3 更新用户偏好

`PUT /users/me/preferences`

请求：

```json
{
  "theme": "light-music",
  "defaultExportFormats": ["midi", "musicxml"],
  "syncPreferences": true
}
```

说明：

- “音频引擎”更像客户端本地硬件配置，建议优先本地存储，不必强制走服务端。
- “清除系统缓存”是客户端行为，不建议设计成服务端 API。

### 6.4 获取我的收藏

`GET /users/me/favorites`

### 6.5 获取我的扒谱历史

`GET /users/me/transcriptions`

### 6.6 获取我的评估历史

`GET /users/me/evaluations`

## 7. 建议优先实现顺序

第一阶段最小闭环：

1. `POST /auth/register`
2. `POST /auth/login`
3. `GET /users/me`
4. `POST /media/upload`
5. `POST /transcriptions`
6. `GET /transcriptions/{jobId}`
7. `GET /scores/{scoreId}`
8. `POST /scores/{scoreId}/exports`

第二阶段：

1. `POST /evaluations`
2. `GET /evaluations/{evaluationId}/report`
3. `POST /evaluations/{evaluationId}/exports`
4. `GET /community/scores`
5. `GET /community/scores/{scoreId}`
6. `POST /community/scores/{scoreId}/comments`

第三阶段：

1. `POST /community/scores`
2. `POST /community/scores/{scoreId}/favorite`
3. `GET /users/me/dashboard`
4. `PUT /users/me/preferences`

## 8. 需要特别说明的地方

- 你的现有前端原型没有真实的数据绑定，所以文档中的字段名是根据界面元素和 README 推导出来的。
- README 同时出现了 `SQLite` 和 `MySQL` 两种描述。结合当前桌面端形态与快速开始说明，推荐先按 `SQLite + EF Core` 设计。
- 社区、评论、收藏、下载这些能力说明项目已经不只是纯本地工具，因此后端数据库要按“多用户共享服务”来规划，而不是只做单机缓存。
