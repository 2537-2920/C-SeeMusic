# SeeMusic 数据库设计稿

本文档基于当前前端界面与 `README.md` 整理，目标是定义一套能支撑“登录注册 + 智能扒谱 + 歌唱评估 + 乐谱社区 + 个人中心”的数据库模型。

说明：

- 推荐第一阶段使用 `SQLite + EF Core`。
- 如果社区规模后续扩大，可以迁移到 `MySQL` 或 `PostgreSQL`，表结构本身可以基本保持一致。
- 当前前端很多交互还只是静态原型，因此部分字段是根据界面行为推导得到的。

## 1. 设计原则

- 用户、乐谱、评估、社区内容分层存储
- 原始文件与导出文件统一走媒体表
- 扒谱和评估采用任务表，便于异步处理
- 音符单独建表，支撑编辑器增删改
- 社区评论、收藏、下载记录独立建表
- 与本机设备强相关的配置优先本地存储，不强行放服务端数据库

## 2. 推荐核心表

### 2.1 `users`

用途：

- 登录注册
- 用户唯一身份

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 用户主键 |
| `username` | TEXT UNIQUE | 用户名 |
| `email` | TEXT UNIQUE | 邮箱 |
| `password_hash` | TEXT | 密码哈希 |
| `password_salt` | TEXT | 可选，若哈希方案需要 |
| `status` | TEXT | `active` / `disabled` |
| `created_at` | DATETIME | 创建时间 |
| `updated_at` | DATETIME | 更新时间 |
| `last_login_at` | DATETIME | 最后登录时间 |

索引建议：

- `idx_users_username`
- `idx_users_email`

### 2.2 `user_profiles`

用途：

- 个人中心展示资料

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `user_id` | INTEGER PK FK | 对应 `users.id` |
| `display_name` | TEXT | 昵称 |
| `avatar_media_id` | INTEGER NULL | 头像资源 |
| `bio` | TEXT NULL | 简介 |
| `created_at` | DATETIME | 创建时间 |
| `updated_at` | DATETIME | 更新时间 |

### 2.3 `refresh_tokens`

用途：

- 登录续期
- 多端会话管理

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `user_id` | INTEGER FK | 用户 ID |
| `token` | TEXT UNIQUE | refresh token |
| `expires_at` | DATETIME | 过期时间 |
| `revoked_at` | DATETIME NULL | 撤销时间 |
| `created_at` | DATETIME | 创建时间 |

## 3. 媒体与文件

### 3.1 `media_assets`

用途：

- 原始音频
- 原始视频
- 参考音频
- 乐谱封面
- 导出 PDF/MIDI/PNG/MusicXML

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `owner_user_id` | INTEGER FK NULL | 上传者 |
| `bucket` | TEXT NULL | 对象存储桶，可选 |
| `storage_path` | TEXT | 存储路径 |
| `original_name` | TEXT | 原文件名 |
| `mime_type` | TEXT | MIME 类型 |
| `file_size` | INTEGER | 字节数 |
| `duration_ms` | INTEGER NULL | 音视频时长 |
| `width` | INTEGER NULL | 图片或视频宽 |
| `height` | INTEGER NULL | 图片或视频高 |
| `media_type` | TEXT | `audio` / `video` / `image` / `document` |
| `created_at` | DATETIME | 创建时间 |

索引建议：

- `idx_media_owner_user_id`
- `idx_media_media_type`

## 4. 扒谱相关

### 4.1 `scores`

用途：

- 乐谱主表
- 同时服务于个人扒谱结果和社区发布内容

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `owner_user_id` | INTEGER FK | 创建者 |
| `title` | TEXT | 标题 |
| `artist_name` | TEXT NULL | 原曲作者/歌手 |
| `arrangement_tag` | TEXT NULL | 如“钢琴版”“简易钢琴” |
| `description` | TEXT NULL | 描述 |
| `source_media_id` | INTEGER NULL FK | 原始音频/视频 |
| `cover_media_id` | INTEGER NULL FK | 封面图 |
| `key_signature` | TEXT NULL | 调号 |
| `time_signature` | TEXT NULL | 拍号，如 `4/4` |
| `tempo` | INTEGER NULL | BPM |
| `status` | TEXT | `draft` / `processing` / `ready` / `published` |
| `source_type` | TEXT | `audio` / `video` / `microphone` / `sample` |
| `is_public` | INTEGER | 0/1 |
| `price_cent` | INTEGER | 价格，单位分 |
| `download_count` | INTEGER | 下载次数 |
| `favorite_count` | INTEGER | 收藏次数 |
| `comment_count` | INTEGER | 评论次数 |
| `created_at` | DATETIME | 创建时间 |
| `updated_at` | DATETIME | 更新时间 |
| `published_at` | DATETIME NULL | 发布时间 |

索引建议：

- `idx_scores_owner_user_id`
- `idx_scores_status`
- `idx_scores_is_public`
- `idx_scores_created_at`

### 4.2 `score_tracks`

用途：

- 支撑“主旋律”“伴奏音轨”等显示

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `score_id` | INTEGER FK | 所属乐谱 |
| `name` | TEXT | 音轨名 |
| `instrument` | TEXT NULL | 乐器名 |
| `channel_no` | INTEGER NULL | MIDI 通道 |
| `sort_order` | INTEGER | 排序 |
| `is_muted` | INTEGER | 是否静音 |
| `is_visible` | INTEGER | 是否显示 |
| `created_at` | DATETIME | 创建时间 |

### 4.3 `score_notes`

用途：

- 编辑器核心数据
- 支撑单音符增删改

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `score_id` | INTEGER FK | 所属乐谱 |
| `track_id` | INTEGER FK | 所属音轨 |
| `measure_no` | INTEGER | 小节号 |
| `beat_start` | REAL | 拍内起始位置 |
| `duration_type` | TEXT | `eighth` / `quarter` / `half` |
| `duration_value` | REAL NULL | 标准化时值，可选 |
| `pitch_name` | TEXT | 如 `C4` |
| `midi_number` | INTEGER | MIDI 音高 |
| `velocity` | INTEGER | 力度 0-127 |
| `staff_x` | REAL NULL | 编辑器横坐标 |
| `staff_y` | REAL NULL | 编辑器纵坐标 |
| `created_at` | DATETIME | 创建时间 |
| `updated_at` | DATETIME | 更新时间 |

索引建议：

- `idx_score_notes_score_id`
- `idx_score_notes_track_id`
- `idx_score_notes_measure_no`

### 4.4 `transcription_jobs`

用途：

- 异步扒谱任务

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `user_id` | INTEGER FK | 发起用户 |
| `score_id` | INTEGER NULL FK | 结果乐谱 |
| `source_media_id` | INTEGER FK | 输入媒体 |
| `source_type` | TEXT | 输入来源 |
| `status` | TEXT | `queued` / `processing` / `succeeded` / `failed` |
| `progress` | INTEGER | 0-100 |
| `separate_melody` | INTEGER | 是否分离旋律 |
| `separate_accompaniment` | INTEGER | 是否分离伴奏 |
| `analyze_rhythm` | INTEGER | 是否分析节奏 |
| `style_hint` | TEXT NULL | 风格提示 |
| `detected_tempo` | INTEGER NULL | 识别 BPM |
| `detected_time_signature` | TEXT NULL | 识别拍号 |
| `error_message` | TEXT NULL | 失败原因 |
| `created_at` | DATETIME | 创建时间 |
| `started_at` | DATETIME NULL | 开始时间 |
| `finished_at` | DATETIME NULL | 完成时间 |

索引建议：

- `idx_transcription_jobs_user_id`
- `idx_transcription_jobs_status`

### 4.5 `score_exports`

用途：

- 记录乐谱导出文件

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `score_id` | INTEGER FK | 所属乐谱 |
| `media_id` | INTEGER FK | 导出文件资源 |
| `export_type` | TEXT | `pdf` / `midi` / `png` / `musicxml` |
| `created_by` | INTEGER FK | 操作者 |
| `created_at` | DATETIME | 创建时间 |

## 5. 歌唱评估相关

### 5.1 `evaluations`

用途：

- 歌唱评估任务主表

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `user_id` | INTEGER FK | 发起用户 |
| `performance_media_id` | INTEGER FK | 演唱文件 |
| `reference_media_id` | INTEGER NULL FK | 原唱或伴奏参考 |
| `status` | TEXT | `queued` / `processing` / `succeeded` / `failed` |
| `total_score` | REAL NULL | 综合得分 |
| `pitch_accuracy` | REAL NULL | 音准准确度 |
| `rhythm_stability` | REAL NULL | 节奏稳定性 |
| `emotion_expression` | REAL NULL | 情感表达 |
| `summary_text` | TEXT NULL | 总结 |
| `error_message` | TEXT NULL | 失败原因 |
| `created_at` | DATETIME | 创建时间 |
| `started_at` | DATETIME NULL | 开始时间 |
| `finished_at` | DATETIME NULL | 完成时间 |

索引建议：

- `idx_evaluations_user_id`
- `idx_evaluations_status`

### 5.2 `evaluation_segments`

用途：

- 存储音准曲线和分段偏差

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `evaluation_id` | INTEGER FK | 所属评估 |
| `segment_type` | TEXT | `pitch` / `rhythm` |
| `start_ms` | INTEGER | 起始时间 |
| `end_ms` | INTEGER | 结束时间 |
| `deviation_value` | REAL NULL | 偏差值 |
| `deviation_unit` | TEXT NULL | `cents` / `ms` |
| `severity` | TEXT | `normal` / `warning` / `critical` |
| `note_text` | TEXT NULL | 分段说明 |

### 5.3 `evaluation_suggestions`

用途：

- 存储“智能纠错建议”

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `evaluation_id` | INTEGER FK | 所属评估 |
| `suggestion_type` | TEXT | `pitch_fix` / `rhythm_fix` / `breath` / `emotion` |
| `title` | TEXT NULL | 标题 |
| `content` | TEXT | 建议内容 |
| `sort_order` | INTEGER | 排序 |

### 5.4 `variation_suggestions`

用途：

- 存储 AI 变奏方案

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `evaluation_id` | INTEGER FK | 所属评估 |
| `style_code` | TEXT | `jazz` / `folk` / `rock` |
| `title` | TEXT | 变奏标题 |
| `description` | TEXT | 说明 |
| `score_preview_id` | INTEGER NULL FK | 可选，若后续生成变奏乐谱 |
| `sort_order` | INTEGER | 排序 |

### 5.5 `evaluation_exports`

用途：

- 导出评估 PDF 报告

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `evaluation_id` | INTEGER FK | 所属评估 |
| `media_id` | INTEGER FK | 导出文件 |
| `export_type` | TEXT | 当前主要为 `pdf` |
| `created_by` | INTEGER FK | 操作者 |
| `created_at` | DATETIME | 创建时间 |

## 6. 社区相关

### 6.1 `score_categories`

用途：

- 精选、流行、古典、爵士、ACG、指弹吉他等分类

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `name` | TEXT UNIQUE | 分类名称 |
| `slug` | TEXT UNIQUE | 分类编码 |
| `sort_order` | INTEGER | 排序 |

### 6.2 `score_category_relations`

用途：

- 乐谱与分类多对多关系

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `score_id` | INTEGER FK | 乐谱 ID |
| `category_id` | INTEGER FK | 分类 ID |

联合主键：

- `score_id + category_id`

### 6.3 `score_comments`

用途：

- 社区评论区

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `score_id` | INTEGER FK | 对应乐谱 |
| `user_id` | INTEGER FK | 评论人 |
| `content` | TEXT | 评论内容 |
| `created_at` | DATETIME | 创建时间 |
| `updated_at` | DATETIME NULL | 更新时间 |
| `status` | TEXT | `visible` / `hidden` / `deleted` |

索引建议：

- `idx_score_comments_score_id`
- `idx_score_comments_user_id`

### 6.4 `score_favorites`

用途：

- 我的收藏
- 详情页收藏按钮

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `user_id` | INTEGER FK | 用户 |
| `score_id` | INTEGER FK | 乐谱 |
| `created_at` | DATETIME | 收藏时间 |

联合主键：

- `user_id + score_id`

### 6.5 `score_downloads`

用途：

- 下载记录
- 热门排序
- 付费校验

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `score_id` | INTEGER FK | 乐谱 |
| `user_id` | INTEGER FK NULL | 下载人，匿名可空 |
| `source_ip` | TEXT NULL | 可选 |
| `created_at` | DATETIME | 下载时间 |

### 6.6 `score_orders`

用途：

- 支撑免费/付费资源
- 校验“立即下载”权限

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | INTEGER PK | 主键 |
| `user_id` | INTEGER FK | 购买用户 |
| `score_id` | INTEGER FK | 乐谱 |
| `amount_cent` | INTEGER | 支付金额 |
| `status` | TEXT | `pending` / `paid` / `cancelled` / `refunded` |
| `created_at` | DATETIME | 创建时间 |
| `paid_at` | DATETIME NULL | 支付时间 |

说明：

- 如果第一阶段不做支付，可以先不建此表，统一按免费资源处理。

## 7. 用户偏好与本地设置

### 7.1 `user_preferences`

用途：

- 同步型偏好设置

字段建议：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `user_id` | INTEGER PK FK | 用户 ID |
| `theme` | TEXT | 主题 |
| `default_export_formats` | TEXT | JSON 数组字符串 |
| `sync_enabled` | INTEGER | 是否启用同步 |
| `updated_at` | DATETIME | 更新时间 |

说明：

- `默认导出格式` 可以放这里。
- `界面主题` 可以放这里。

### 7.2 不建议放服务端数据库的内容

以下内容更适合保存在客户端本地配置文件或本地 SQLite：

- 当前机器的音频设备选择
- 本地缓存占用大小
- 临时导出目录
- 最近打开文件路径
- UI 窗口布局状态

原因：

- 强依赖本机环境
- 换设备后不一定有效
- 不属于共享业务数据

## 8. 表关系总览

核心关系：

- `users 1 -> 1 user_profiles`
- `users 1 -> n refresh_tokens`
- `users 1 -> n scores`
- `scores 1 -> n score_tracks`
- `score_tracks 1 -> n score_notes`
- `scores 1 -> n score_exports`
- `users 1 -> n transcription_jobs`
- `scores 1 -> n score_comments`
- `users n <-> n scores` 通过 `score_favorites`
- `users 1 -> n evaluations`
- `evaluations 1 -> n evaluation_segments`
- `evaluations 1 -> n evaluation_suggestions`
- `evaluations 1 -> n variation_suggestions`

## 9. 推荐最小落地版本

如果你现在要优先支撑已有界面，第一批只需要这些表：

1. `users`
2. `user_profiles`
3. `refresh_tokens`
4. `media_assets`
5. `scores`
6. `score_tracks`
7. `score_notes`
8. `transcription_jobs`
9. `evaluations`
10. `evaluation_segments`
11. `evaluation_suggestions`
12. `variation_suggestions`
13. `score_categories`
14. `score_category_relations`
15. `score_comments`
16. `score_favorites`
17. `score_downloads`
18. `user_preferences`

## 10. 与当前前端的对应关系

登录/注册：

- `users`
- `user_profiles`
- `refresh_tokens`

智能扒谱页：

- `media_assets`
- `transcription_jobs`
- `scores`
- `score_tracks`
- `score_notes`
- `score_exports`

歌唱评估页：

- `media_assets`
- `evaluations`
- `evaluation_segments`
- `evaluation_suggestions`
- `variation_suggestions`
- `evaluation_exports`

社区页：

- `scores`
- `score_categories`
- `score_category_relations`
- `score_comments`
- `score_favorites`
- `score_downloads`
- `score_orders`

个人中心页：

- `user_profiles`
- `user_preferences`
- 各业务表的统计聚合结果

## 11. 额外建议

- 乐谱音符如果后续要支持撤销/重做与版本管理，可以补一张 `score_revisions` 表。
- 如果后续扒谱和评估都要跑长任务，建议再补一张统一的 `job_logs` 表记录进度日志。
- 如果后续支持多端同步和云存储，`media_assets.storage_path` 最好从一开始就不要写死本地绝对路径。
