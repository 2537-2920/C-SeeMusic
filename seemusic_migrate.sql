-- MySQL dump 10.13  Distrib 9.6.0, for macos15 (arm64)
--
-- Host: localhost    Database: SeeMusic_1
-- ------------------------------------------------------
-- Server version	9.6.0

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
SET @MYSQLDUMP_TEMP_LOG_BIN = @@SESSION.SQL_LOG_BIN;
SET @@SESSION.SQL_LOG_BIN= 0;

--
-- GTID state at the beginning of the backup 
--

SET @@GLOBAL.GTID_PURGED=/*!80000 '+'*/ '117cd8fe-34f5-11f1-8999-0f2aa5bdc399:1-76';

--
-- Table structure for table `evaluation_exports`
--

DROP TABLE IF EXISTS `evaluation_exports`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `evaluation_exports` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `evaluation_id` int unsigned NOT NULL COMMENT '所属评估ID',
  `media_id` int unsigned NOT NULL COMMENT '导出文件ID',
  `export_type` varchar(20) NOT NULL DEFAULT 'pdf' COMMENT '导出类型',
  `created_by` int unsigned NOT NULL COMMENT '操作者ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_evaluation_exports_evaluation_id` (`evaluation_id`),
  KEY `fk_evaluation_exports_media_id` (`media_id`),
  KEY `fk_evaluation_exports_created_by` (`created_by`),
  CONSTRAINT `fk_evaluation_exports_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_evaluation_exports_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `evaluations` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_evaluation_exports_media_id` FOREIGN KEY (`media_id`) REFERENCES `media_assets` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='评估导出表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `evaluation_exports`
--

LOCK TABLES `evaluation_exports` WRITE;
/*!40000 ALTER TABLE `evaluation_exports` DISABLE KEYS */;
/*!40000 ALTER TABLE `evaluation_exports` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `evaluation_segments`
--

DROP TABLE IF EXISTS `evaluation_segments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `evaluation_segments` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `evaluation_id` int unsigned NOT NULL COMMENT '所属评估ID',
  `segment_type` varchar(20) NOT NULL COMMENT '类型：pitch/rhythm',
  `start_ms` int unsigned NOT NULL COMMENT '起始时间（毫秒）',
  `end_ms` int unsigned NOT NULL COMMENT '结束时间（毫秒）',
  `deviation_value` float DEFAULT NULL COMMENT '偏差值',
  `deviation_unit` varchar(10) DEFAULT NULL COMMENT '单位：cents/ms',
  `severity` varchar(20) NOT NULL DEFAULT 'normal' COMMENT '严重程度：normal/warning/critical',
  `note_text` text COMMENT '分段说明',
  PRIMARY KEY (`id`),
  KEY `idx_evaluation_segments_evaluation_id` (`evaluation_id`),
  CONSTRAINT `fk_evaluation_segments_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `evaluations` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='评估分段表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `evaluation_segments`
--

LOCK TABLES `evaluation_segments` WRITE;
/*!40000 ALTER TABLE `evaluation_segments` DISABLE KEYS */;
/*!40000 ALTER TABLE `evaluation_segments` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `evaluation_suggestions`
--

DROP TABLE IF EXISTS `evaluation_suggestions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `evaluation_suggestions` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `evaluation_id` int unsigned NOT NULL COMMENT '所属评估ID',
  `suggestion_type` varchar(20) NOT NULL COMMENT '类型：pitch_fix/rhythm_fix/breath/emotion',
  `title` varchar(100) DEFAULT NULL COMMENT '标题',
  `content` text NOT NULL COMMENT '建议内容',
  `sort_order` int unsigned NOT NULL DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`id`),
  KEY `idx_evaluation_suggestions_evaluation_id` (`evaluation_id`),
  CONSTRAINT `fk_evaluation_suggestions_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `evaluations` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='评估建议表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `evaluation_suggestions`
--

LOCK TABLES `evaluation_suggestions` WRITE;
/*!40000 ALTER TABLE `evaluation_suggestions` DISABLE KEYS */;
/*!40000 ALTER TABLE `evaluation_suggestions` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `evaluations`
--

DROP TABLE IF EXISTS `evaluations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `evaluations` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `user_id` int unsigned NOT NULL COMMENT '发起用户ID',
  `performance_media_id` int unsigned NOT NULL COMMENT '演唱文件ID',
  `reference_media_id` int unsigned DEFAULT NULL COMMENT '参考音频ID',
  `status` varchar(20) NOT NULL DEFAULT 'queued' COMMENT '状态：queued/processing/succeeded/failed',
  `total_score` float DEFAULT NULL COMMENT '综合得分',
  `pitch_accuracy` float DEFAULT NULL COMMENT '音准准确度',
  `rhythm_stability` float DEFAULT NULL COMMENT '节奏稳定性',
  `emotion_expression` float DEFAULT NULL COMMENT '情感表达',
  `summary_text` text COMMENT '总结',
  `error_message` text COMMENT '失败原因',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `started_at` datetime DEFAULT NULL COMMENT '开始时间',
  `finished_at` datetime DEFAULT NULL COMMENT '完成时间',
  PRIMARY KEY (`id`),
  KEY `idx_evaluations_user_id` (`user_id`),
  KEY `idx_evaluations_status` (`status`),
  KEY `fk_evaluations_performance_media_id` (`performance_media_id`),
  KEY `fk_evaluations_reference_media_id` (`reference_media_id`),
  CONSTRAINT `fk_evaluations_performance_media_id` FOREIGN KEY (`performance_media_id`) REFERENCES `media_assets` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_evaluations_reference_media_id` FOREIGN KEY (`reference_media_id`) REFERENCES `media_assets` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_evaluations_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='歌唱评估表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `evaluations`
--

LOCK TABLES `evaluations` WRITE;
/*!40000 ALTER TABLE `evaluations` DISABLE KEYS */;
/*!40000 ALTER TABLE `evaluations` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `media_assets`
--

DROP TABLE IF EXISTS `media_assets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `media_assets` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `owner_user_id` int unsigned DEFAULT NULL COMMENT '上传者ID',
  `bucket` varchar(100) DEFAULT NULL COMMENT '对象存储桶',
  `storage_path` varchar(500) NOT NULL COMMENT '存储路径',
  `original_name` varchar(255) NOT NULL COMMENT '原文件名',
  `mime_type` varchar(100) NOT NULL COMMENT 'MIME类型',
  `file_size` int unsigned NOT NULL COMMENT '文件大小（字节）',
  `duration_ms` int unsigned DEFAULT NULL COMMENT '音视频时长（毫秒）',
  `width` int unsigned DEFAULT NULL COMMENT '宽',
  `height` int unsigned DEFAULT NULL COMMENT '高',
  `media_type` varchar(20) NOT NULL COMMENT '类型：audio/video/image/document',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_media_owner_user_id` (`owner_user_id`),
  KEY `idx_media_media_type` (`media_type`),
  CONSTRAINT `fk_media_assets_owner_user_id` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='媒体资源表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `media_assets`
--

LOCK TABLES `media_assets` WRITE;
/*!40000 ALTER TABLE `media_assets` DISABLE KEYS */;
/*!40000 ALTER TABLE `media_assets` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `refresh_tokens`
--

DROP TABLE IF EXISTS `refresh_tokens`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `refresh_tokens` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `user_id` int unsigned NOT NULL COMMENT '用户ID',
  `token` varchar(255) NOT NULL COMMENT 'refresh token',
  `expires_at` datetime NOT NULL COMMENT '过期时间',
  `revoked_at` datetime DEFAULT NULL COMMENT '撤销时间',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_refresh_token` (`token`),
  KEY `idx_refresh_tokens_user_id` (`user_id`),
  CONSTRAINT `fk_refresh_tokens_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='刷新令牌表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `refresh_tokens`
--

LOCK TABLES `refresh_tokens` WRITE;
/*!40000 ALTER TABLE `refresh_tokens` DISABLE KEYS */;
/*!40000 ALTER TABLE `refresh_tokens` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_categories`
--

DROP TABLE IF EXISTS `score_categories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_categories` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `name` varchar(50) NOT NULL COMMENT '分类名称',
  `slug` varchar(50) NOT NULL COMMENT '分类编码',
  `sort_order` int unsigned NOT NULL DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_score_categories_name` (`name`),
  UNIQUE KEY `idx_score_categories_slug` (`slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱分类表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_categories`
--

LOCK TABLES `score_categories` WRITE;
/*!40000 ALTER TABLE `score_categories` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_categories` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_category_relations`
--

DROP TABLE IF EXISTS `score_category_relations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_category_relations` (
  `score_id` int unsigned NOT NULL COMMENT '乐谱ID',
  `category_id` int unsigned NOT NULL COMMENT '分类ID',
  PRIMARY KEY (`score_id`,`category_id`),
  KEY `idx_score_category_relations_category_id` (`category_id`),
  CONSTRAINT `fk_score_category_relations_category_id` FOREIGN KEY (`category_id`) REFERENCES `score_categories` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_category_relations_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱分类关联表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_category_relations`
--

LOCK TABLES `score_category_relations` WRITE;
/*!40000 ALTER TABLE `score_category_relations` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_category_relations` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_comments`
--

DROP TABLE IF EXISTS `score_comments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_comments` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `score_id` int unsigned NOT NULL COMMENT '乐谱ID',
  `user_id` int unsigned NOT NULL COMMENT '评论人ID',
  `content` text NOT NULL COMMENT '评论内容',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `status` varchar(20) NOT NULL DEFAULT 'visible' COMMENT '状态：visible/hidden/deleted',
  PRIMARY KEY (`id`),
  KEY `idx_score_comments_score_id` (`score_id`),
  KEY `idx_score_comments_user_id` (`user_id`),
  CONSTRAINT `fk_score_comments_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_comments_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱评论表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_comments`
--

LOCK TABLES `score_comments` WRITE;
/*!40000 ALTER TABLE `score_comments` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_comments` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_downloads`
--

DROP TABLE IF EXISTS `score_downloads`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_downloads` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `score_id` int unsigned NOT NULL COMMENT '乐谱ID',
  `user_id` int unsigned DEFAULT NULL COMMENT '下载人ID（匿名可空）',
  `source_ip` varchar(50) DEFAULT NULL COMMENT '来源IP',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '下载时间',
  PRIMARY KEY (`id`),
  KEY `idx_score_downloads_score_id` (`score_id`),
  KEY `idx_score_downloads_user_id` (`user_id`),
  CONSTRAINT `fk_score_downloads_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_downloads_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱下载表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_downloads`
--

LOCK TABLES `score_downloads` WRITE;
/*!40000 ALTER TABLE `score_downloads` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_downloads` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_exports`
--

DROP TABLE IF EXISTS `score_exports`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_exports` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `score_id` int unsigned NOT NULL COMMENT '所属乐谱ID',
  `media_id` int unsigned NOT NULL COMMENT '导出文件资源ID',
  `export_type` varchar(20) NOT NULL COMMENT '导出类型：pdf/midi/png/musicxml',
  `created_by` int unsigned NOT NULL COMMENT '操作者ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_score_exports_score_id` (`score_id`),
  KEY `fk_score_exports_media_id` (`media_id`),
  KEY `fk_score_exports_created_by` (`created_by`),
  CONSTRAINT `fk_score_exports_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_exports_media_id` FOREIGN KEY (`media_id`) REFERENCES `media_assets` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_exports_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱导出表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_exports`
--

LOCK TABLES `score_exports` WRITE;
/*!40000 ALTER TABLE `score_exports` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_exports` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_favorites`
--

DROP TABLE IF EXISTS `score_favorites`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_favorites` (
  `user_id` int unsigned NOT NULL COMMENT '用户ID',
  `score_id` int unsigned NOT NULL COMMENT '乐谱ID',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '收藏时间',
  PRIMARY KEY (`user_id`,`score_id`),
  KEY `idx_score_favorites_score_id` (`score_id`),
  CONSTRAINT `fk_score_favorites_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_favorites_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱收藏表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_favorites`
--

LOCK TABLES `score_favorites` WRITE;
/*!40000 ALTER TABLE `score_favorites` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_favorites` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_notes`
--

DROP TABLE IF EXISTS `score_notes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_notes` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `score_id` int unsigned NOT NULL COMMENT '所属乐谱ID',
  `track_id` int unsigned NOT NULL COMMENT '所属音轨ID',
  `measure_no` int unsigned NOT NULL COMMENT '小节号',
  `beat_start` float NOT NULL COMMENT '拍内起始位置',
  `duration_type` varchar(20) NOT NULL COMMENT '时值类型：eighth/quarter/half等',
  `duration_value` float DEFAULT NULL COMMENT '标准化时值',
  `pitch_name` varchar(10) NOT NULL COMMENT '音高名称（如C4）',
  `midi_number` int unsigned NOT NULL COMMENT 'MIDI音高',
  `velocity` int unsigned NOT NULL DEFAULT '64' COMMENT '力度（0-127）',
  `staff_x` float DEFAULT NULL COMMENT '编辑器横坐标',
  `staff_y` float DEFAULT NULL COMMENT '编辑器纵坐标',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  KEY `idx_score_notes_score_id` (`score_id`),
  KEY `idx_score_notes_track_id` (`track_id`),
  KEY `idx_score_notes_measure_no` (`measure_no`),
  CONSTRAINT `fk_score_notes_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_notes_track_id` FOREIGN KEY (`track_id`) REFERENCES `score_tracks` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱音符表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_notes`
--

LOCK TABLES `score_notes` WRITE;
/*!40000 ALTER TABLE `score_notes` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_notes` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_orders`
--

DROP TABLE IF EXISTS `score_orders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_orders` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `user_id` int unsigned NOT NULL COMMENT '购买用户ID',
  `score_id` int unsigned NOT NULL COMMENT '乐谱ID',
  `amount_cent` int unsigned NOT NULL COMMENT '支付金额（分）',
  `status` varchar(20) NOT NULL DEFAULT 'pending' COMMENT '状态：pending/paid/cancelled/refunded',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `paid_at` datetime DEFAULT NULL COMMENT '支付时间',
  PRIMARY KEY (`id`),
  KEY `idx_score_orders_user_id` (`user_id`),
  KEY `idx_score_orders_score_id` (`score_id`),
  KEY `idx_score_orders_status` (`status`),
  CONSTRAINT `fk_score_orders_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_score_orders_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱订单表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_orders`
--

LOCK TABLES `score_orders` WRITE;
/*!40000 ALTER TABLE `score_orders` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_orders` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `score_tracks`
--

DROP TABLE IF EXISTS `score_tracks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `score_tracks` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `score_id` int unsigned NOT NULL COMMENT '所属乐谱ID',
  `name` varchar(50) NOT NULL COMMENT '音轨名',
  `instrument` varchar(50) DEFAULT NULL COMMENT '乐器名',
  `channel_no` int unsigned DEFAULT NULL COMMENT 'MIDI通道',
  `sort_order` int unsigned NOT NULL DEFAULT '0' COMMENT '排序',
  `is_muted` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '是否静音：0/1',
  `is_visible` tinyint unsigned NOT NULL DEFAULT '1' COMMENT '是否显示：0/1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_score_tracks_score_id` (`score_id`),
  CONSTRAINT `fk_score_tracks_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱音轨表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `score_tracks`
--

LOCK TABLES `score_tracks` WRITE;
/*!40000 ALTER TABLE `score_tracks` DISABLE KEYS */;
/*!40000 ALTER TABLE `score_tracks` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `scores`
--

DROP TABLE IF EXISTS `scores`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `scores` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `owner_user_id` int unsigned NOT NULL COMMENT '创建者ID',
  `title` varchar(100) NOT NULL COMMENT '标题',
  `artist_name` varchar(100) DEFAULT NULL COMMENT '原曲作者/歌手',
  `arrangement_tag` varchar(50) DEFAULT NULL COMMENT '改编标签',
  `description` text COMMENT '描述',
  `source_media_id` int unsigned DEFAULT NULL COMMENT '原始音视频ID',
  `cover_media_id` int unsigned DEFAULT NULL COMMENT '封面图ID',
  `key_signature` varchar(20) DEFAULT NULL COMMENT '调号',
  `time_signature` varchar(10) DEFAULT NULL COMMENT '拍号',
  `tempo` int unsigned DEFAULT NULL COMMENT 'BPM',
  `status` varchar(20) NOT NULL DEFAULT 'draft' COMMENT '状态：draft/processing/ready/published',
  `source_type` varchar(20) NOT NULL COMMENT '来源：audio/video/microphone/sample',
  `is_public` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '是否公开：0/1',
  `price_cent` int unsigned NOT NULL DEFAULT '0' COMMENT '价格（分）',
  `download_count` int unsigned NOT NULL DEFAULT '0' COMMENT '下载次数',
  `favorite_count` int unsigned NOT NULL DEFAULT '0' COMMENT '收藏次数',
  `comment_count` int unsigned NOT NULL DEFAULT '0' COMMENT '评论次数',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `published_at` datetime DEFAULT NULL COMMENT '发布时间',
  PRIMARY KEY (`id`),
  KEY `idx_scores_owner_user_id` (`owner_user_id`),
  KEY `idx_scores_status` (`status`),
  KEY `idx_scores_is_public` (`is_public`),
  KEY `idx_scores_created_at` (`created_at`),
  KEY `fk_scores_source_media_id` (`source_media_id`),
  KEY `fk_scores_cover_media_id` (`cover_media_id`),
  CONSTRAINT `fk_scores_cover_media_id` FOREIGN KEY (`cover_media_id`) REFERENCES `media_assets` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_scores_owner_user_id` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_scores_source_media_id` FOREIGN KEY (`source_media_id`) REFERENCES `media_assets` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='乐谱主表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `scores`
--

LOCK TABLES `scores` WRITE;
/*!40000 ALTER TABLE `scores` DISABLE KEYS */;
/*!40000 ALTER TABLE `scores` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `transcription_jobs`
--

DROP TABLE IF EXISTS `transcription_jobs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `transcription_jobs` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `user_id` int unsigned NOT NULL COMMENT '发起用户ID',
  `score_id` int unsigned DEFAULT NULL COMMENT '结果乐谱ID',
  `source_media_id` int unsigned NOT NULL COMMENT '输入媒体ID',
  `source_type` varchar(20) NOT NULL COMMENT '输入来源',
  `status` varchar(20) NOT NULL DEFAULT 'queued' COMMENT '状态：queued/processing/succeeded/failed',
  `progress` int unsigned NOT NULL DEFAULT '0' COMMENT '进度（0-100）',
  `separate_melody` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '是否分离旋律：0/1',
  `separate_accompaniment` tinyint unsigned NOT NULL DEFAULT '0' COMMENT '是否分离伴奏：0/1',
  `analyze_rhythm` tinyint unsigned NOT NULL DEFAULT '1' COMMENT '是否分析节奏：0/1',
  `style_hint` varchar(100) DEFAULT NULL COMMENT '风格提示',
  `detected_tempo` int unsigned DEFAULT NULL COMMENT '识别BPM',
  `detected_time_signature` varchar(10) DEFAULT NULL COMMENT '识别拍号',
  `error_message` text COMMENT '失败原因',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `started_at` datetime DEFAULT NULL COMMENT '开始时间',
  `finished_at` datetime DEFAULT NULL COMMENT '完成时间',
  PRIMARY KEY (`id`),
  KEY `idx_transcription_jobs_user_id` (`user_id`),
  KEY `idx_transcription_jobs_status` (`status`),
  KEY `fk_transcription_jobs_score_id` (`score_id`),
  KEY `fk_transcription_jobs_source_media_id` (`source_media_id`),
  CONSTRAINT `fk_transcription_jobs_score_id` FOREIGN KEY (`score_id`) REFERENCES `scores` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_transcription_jobs_source_media_id` FOREIGN KEY (`source_media_id`) REFERENCES `media_assets` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_transcription_jobs_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='扒谱任务表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `transcription_jobs`
--

LOCK TABLES `transcription_jobs` WRITE;
/*!40000 ALTER TABLE `transcription_jobs` DISABLE KEYS */;
/*!40000 ALTER TABLE `transcription_jobs` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `user_preferences`
--

DROP TABLE IF EXISTS `user_preferences`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `user_preferences` (
  `user_id` int unsigned NOT NULL COMMENT '用户ID',
  `theme` varchar(50) NOT NULL DEFAULT 'light' COMMENT '主题',
  `default_export_formats` text COMMENT '默认导出格式（JSON数组）',
  `sync_enabled` tinyint unsigned NOT NULL DEFAULT '1' COMMENT '是否启用同步：0/1',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`user_id`),
  CONSTRAINT `fk_user_preferences_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户偏好表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `user_preferences`
--

LOCK TABLES `user_preferences` WRITE;
/*!40000 ALTER TABLE `user_preferences` DISABLE KEYS */;
/*!40000 ALTER TABLE `user_preferences` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `user_profiles`
--

DROP TABLE IF EXISTS `user_profiles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `user_profiles` (
  `user_id` int unsigned NOT NULL COMMENT '关联users.id',
  `display_name` varchar(50) DEFAULT NULL COMMENT '昵称',
  `avatar_media_id` int unsigned DEFAULT NULL COMMENT '头像资源ID',
  `bio` text COMMENT '简介',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`user_id`),
  CONSTRAINT `fk_user_profiles_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户资料表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `user_profiles`
--

LOCK TABLES `user_profiles` WRITE;
/*!40000 ALTER TABLE `user_profiles` DISABLE KEYS */;
/*!40000 ALTER TABLE `user_profiles` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '用户主键',
  `username` varchar(50) NOT NULL COMMENT '用户名',
  `email` varchar(100) NOT NULL COMMENT '邮箱',
  `password_hash` varchar(255) NOT NULL COMMENT '密码哈希',
  `password_salt` varchar(255) DEFAULT NULL COMMENT '密码盐',
  `status` varchar(20) NOT NULL DEFAULT 'active' COMMENT '状态：active/disabled',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  `last_login_at` datetime DEFAULT NULL COMMENT '最后登录时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_users_username` (`username`),
  UNIQUE KEY `idx_users_email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='用户表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `variation_suggestions`
--

DROP TABLE IF EXISTS `variation_suggestions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `variation_suggestions` (
  `id` int unsigned NOT NULL AUTO_INCREMENT COMMENT '主键',
  `evaluation_id` int unsigned NOT NULL COMMENT '所属评估ID',
  `style_code` varchar(20) NOT NULL COMMENT '风格：jazz/folk/rock等',
  `title` varchar(100) NOT NULL COMMENT '变奏标题',
  `description` text COMMENT '说明',
  `score_preview_id` int unsigned DEFAULT NULL COMMENT '变奏乐谱预览ID',
  `sort_order` int unsigned NOT NULL DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`id`),
  KEY `idx_variation_suggestions_evaluation_id` (`evaluation_id`),
  KEY `fk_variation_suggestions_score_preview_id` (`score_preview_id`),
  CONSTRAINT `fk_variation_suggestions_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `evaluations` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_variation_suggestions_score_preview_id` FOREIGN KEY (`score_preview_id`) REFERENCES `scores` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='变奏建议表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `variation_suggestions`
--

LOCK TABLES `variation_suggestions` WRITE;
/*!40000 ALTER TABLE `variation_suggestions` DISABLE KEYS */;
/*!40000 ALTER TABLE `variation_suggestions` ENABLE KEYS */;
UNLOCK TABLES;
SET @@SESSION.SQL_LOG_BIN = @MYSQLDUMP_TEMP_LOG_BIN;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-04-14 13:30:49
