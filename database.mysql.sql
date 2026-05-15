-- SeeMusic current backend schema
-- Source of truth: backend/Models/Entities.cs + backend/Data/SeeMusicDbContext.cs
-- Database engine: MySQL 8.x / MariaDB 10.6+
-- Charset: utf8mb4
--
-- Important:
-- 1. Current backend uses MySQL via Pomelo.EntityFrameworkCore.MySql.
-- 2. This file now contains:
--    - the current runtime core schema used by backend
--    - compatibility/extension tables for community and profile features
-- 3. There are currently no EF Core migrations in the repo. If your database was
--    created from an older structure, the safest path is to back up data and
--    recreate the schema with this file.

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE TABLE IF NOT EXISTS `Users` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Username` varchar(50) NOT NULL,
  `Email` varchar(100) NOT NULL,
  `PasswordHash` longtext NOT NULL,
  `PasswordSalt` longtext NULL,
  `DisplayName` varchar(100) NOT NULL,
  `AvatarUrl` longtext NULL,
  `Bio` varchar(500) NOT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'active',
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  `LastLoginAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Users_Username` (`Username`),
  UNIQUE KEY `IX_Users_Email` (`Email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `MediaFiles` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `MediaId` varchar(50) NOT NULL,
  `UserId` int NULL,
  `Bucket` varchar(100) NULL,
  `FileName` varchar(255) NOT NULL,
  `Type` varchar(20) NOT NULL,
  `MimeType` varchar(100) NOT NULL,
  `FileSize` bigint NOT NULL,
  `Url` longtext NOT NULL,
  `StoragePath` varchar(255) NOT NULL,
  `DurationMs` int NULL,
  `Width` int NULL,
  `Height` int NULL,
  `MediaType` varchar(20) NULL,
  `PreparedAudioStatus` varchar(20) NOT NULL,
  `PreparedAudioPath` varchar(255) NULL,
  `PreparationErrorMessage` varchar(500) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_MediaFiles_MediaId` (`MediaId`),
  KEY `IX_MediaFiles_UserId` (`UserId`),
  CONSTRAINT `FK_MediaFiles_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `RefreshTokens` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `Token` longtext NOT NULL,
  `ExpiresAt` datetime(6) NOT NULL,
  `RevokedAt` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_RefreshTokens_UserId` (`UserId`),
  CONSTRAINT `FK_RefreshTokens_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Evaluations` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `EvaluationId` varchar(50) NOT NULL,
  `UserId` int NULL,
  `PerformanceMediaFileId` int NOT NULL,
  `ReferenceMediaFileId` int NULL,
  `Status` varchar(20) NOT NULL,
  `Progress` int NOT NULL,
  `AnalyzePitch` tinyint(1) NOT NULL,
  `AnalyzeRhythm` tinyint(1) NOT NULL,
  `ScoringProfile` varchar(40) NOT NULL,
  `PitchStatus` varchar(20) NOT NULL,
  `RhythmStatus` varchar(20) NOT NULL,
  `TotalScore` double NULL,
  `PitchScore` double NULL,
  `RhythmScore` double NULL,
  `DetectedTempoBpm` double NULL,
  `MeanPitchDeviationCents` double NULL,
  `Badge` varchar(30) NOT NULL,
  `SummaryText` varchar(1000) NOT NULL,
  `OptionsJson` longtext NOT NULL,
  `WarningMessagesJson` longtext NOT NULL,
  `PitchAnalysisJson` longtext NOT NULL,
  `RhythmAnalysisJson` longtext NOT NULL,
  `TransposeBaseJson` longtext NOT NULL,
  `ErrorMessage` varchar(1000) NOT NULL,
  `AnonymousTokenHash` varchar(120) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `StartedAt` datetime(6) NULL,
  `FinishedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Evaluations_EvaluationId` (`EvaluationId`),
  KEY `IX_Evaluations_UserId` (`UserId`),
  KEY `IX_Evaluations_Status` (`Status`),
  KEY `IX_Evaluations_PerformanceMediaFileId` (`PerformanceMediaFileId`),
  KEY `IX_Evaluations_ReferenceMediaFileId` (`ReferenceMediaFileId`),
  CONSTRAINT `FK_Evaluations_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_Evaluations_MediaFiles_PerformanceMediaFileId`
    FOREIGN KEY (`PerformanceMediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_Evaluations_MediaFiles_ReferenceMediaFileId`
    FOREIGN KEY (`ReferenceMediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `EvaluationSegments` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `EvaluationDbId` int NOT NULL,
  `MetricType` varchar(20) NOT NULL,
  `StartMs` int NOT NULL,
  `EndMs` int NOT NULL,
  `Score` double NULL,
  `DeviationValue` double NULL,
  `DeviationUnit` varchar(20) NULL,
  `Severity` varchar(20) NOT NULL,
  `NoteText` varchar(500) NOT NULL,
  `SortOrder` int NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_EvaluationSegments_EvaluationDbId_MetricType_SortOrder` (`EvaluationDbId`, `MetricType`, `SortOrder`),
  CONSTRAINT `FK_EvaluationSegments_Evaluations_EvaluationDbId`
    FOREIGN KEY (`EvaluationDbId`) REFERENCES `Evaluations` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `EvaluationSuggestions` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `EvaluationDbId` int NOT NULL,
  `SuggestionType` varchar(30) NOT NULL,
  `Title` varchar(120) NOT NULL,
  `Content` varchar(1000) NOT NULL,
  `SortOrder` int NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_EvaluationSuggestions_EvaluationDbId_SortOrder` (`EvaluationDbId`, `SortOrder`),
  CONSTRAINT `FK_EvaluationSuggestions_Evaluations_EvaluationDbId`
    FOREIGN KEY (`EvaluationDbId`) REFERENCES `Evaluations` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `EvaluationExports` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `EvaluationDbId` int NOT NULL,
  `MediaFileId` int NOT NULL,
  `ExportType` varchar(20) NOT NULL,
  `CreatedByUserId` int NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_EvaluationExports_EvaluationDbId` (`EvaluationDbId`),
  KEY `IX_EvaluationExports_MediaFileId` (`MediaFileId`),
  KEY `IX_EvaluationExports_CreatedByUserId` (`CreatedByUserId`),
  CONSTRAINT `FK_EvaluationExports_Evaluations_EvaluationDbId`
    FOREIGN KEY (`EvaluationDbId`) REFERENCES `Evaluations` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_EvaluationExports_MediaFiles_MediaFileId`
    FOREIGN KEY (`MediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_EvaluationExports_Users_CreatedByUserId`
    FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Scores` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScoreId` varchar(50) NOT NULL,
  `UserId` int NULL,
  `OwnerUserId` int NULL,
  `SourceMediaFileId` int NOT NULL,
  `CoverMediaFileId` int NULL,
  `Title` varchar(200) NOT NULL,
  `ArtistName` varchar(200) NULL,
  `ArrangementTag` varchar(100) NULL,
  `Description` longtext NULL,
  `InstrumentMode` varchar(30) NOT NULL,
  `Status` varchar(20) NOT NULL,
  `SourceType` varchar(20) NULL,
  `IsPublic` tinyint(1) NOT NULL DEFAULT 0,
  `PriceCent` int NOT NULL DEFAULT 0,
  `DownloadCount` int NOT NULL DEFAULT 0,
  `FavoriteCount` int NOT NULL DEFAULT 0,
  `CommentCount` int NOT NULL DEFAULT 0,
  `TempoBpm` double NULL,
  `TimeSignature` varchar(20) NOT NULL,
  `KeySignature` varchar(20) NOT NULL,
  `MeasureCount` int NOT NULL,
  `EstimatedPageCount` int NOT NULL,
  `MusicXmlContent` longtext NOT NULL,
  `AnalysisSummaryJson` longtext NOT NULL,
  `WarningMessagesJson` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `PublishedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Scores_ScoreId` (`ScoreId`),
  KEY `IX_Scores_UserId` (`UserId`),
  KEY `IX_Scores_OwnerUserId` (`OwnerUserId`),
  KEY `IX_Scores_SourceMediaFileId` (`SourceMediaFileId`),
  KEY `IX_Scores_CoverMediaFileId` (`CoverMediaFileId`),
  KEY `IX_Scores_IsPublic` (`IsPublic`),
  CONSTRAINT `FK_Scores_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_Scores_Users_OwnerUserId`
    FOREIGN KEY (`OwnerUserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_Scores_MediaFiles_SourceMediaFileId`
    FOREIGN KEY (`SourceMediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_Scores_MediaFiles_CoverMediaFileId`
    FOREIGN KEY (`CoverMediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreTracks` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScoreDbId` int NOT NULL,
  `Name` varchar(80) NOT NULL,
  `HandRole` varchar(20) NOT NULL,
  `Instrument` varchar(40) NOT NULL,
  `ChannelNo` int NULL,
  `NoteCount` int NOT NULL,
  `RangeLowMidi` int NULL,
  `RangeHighMidi` int NULL,
  `IsMuted` tinyint(1) NOT NULL DEFAULT 0,
  `IsVisible` tinyint(1) NOT NULL DEFAULT 1,
  `IsGenerated` tinyint(1) NOT NULL,
  `SummaryText` varchar(500) NOT NULL,
  `SortOrder` int NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  KEY `IX_ScoreTracks_ScoreDbId_SortOrder` (`ScoreDbId`, `SortOrder`),
  CONSTRAINT `FK_ScoreTracks_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreNotes` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScoreDbId` int NOT NULL,
  `ScoreTrackDbId` int NOT NULL,
  `MeasureNo` int NOT NULL,
  `BeatStart` double NOT NULL,
  `DurationType` varchar(20) NOT NULL,
  `DurationBeats` double NOT NULL,
  `DurationValue` double NULL,
  `PitchName` varchar(10) NOT NULL,
  `MidiNumber` int NOT NULL,
  `Velocity` int NOT NULL DEFAULT 64,
  `Staff` varchar(20) NOT NULL,
  `StartTimeSeconds` double NOT NULL,
  `IsChordTone` tinyint(1) NOT NULL,
  `StaffX` double NULL,
  `StaffY` double NULL,
  `SortOrder` int NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  KEY `IX_ScoreNotes_ScoreDbId_ScoreTrackDbId_MeasureNo_SortOrder` (`ScoreDbId`, `ScoreTrackDbId`, `MeasureNo`, `SortOrder`),
  CONSTRAINT `FK_ScoreNotes_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreNotes_ScoreTracks_ScoreTrackDbId`
    FOREIGN KEY (`ScoreTrackDbId`) REFERENCES `ScoreTracks` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `TranscriptionJobs` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `JobId` varchar(50) NOT NULL,
  `UserId` int NULL,
  `SourceMediaFileId` int NOT NULL,
  `ScoreDbId` int NULL,
  `ProjectTitle` varchar(200) NOT NULL,
  `SourceType` varchar(20) NOT NULL,
  `Status` varchar(20) NOT NULL,
  `Progress` int NOT NULL,
  `OptionsJson` longtext NOT NULL,
  `ErrorMessage` varchar(1000) NOT NULL,
  `DetectedTempoBpm` double NULL,
  `DetectedTimeSignature` varchar(20) NULL,
  `MeasureCount` int NULL,
  `EstimatedPageCount` int NULL,
  `BeatAnalysisJson` longtext NOT NULL,
  `WarningMessagesJson` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NOT NULL,
  `StartedAt` datetime(6) NULL,
  `FinishedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_TranscriptionJobs_JobId` (`JobId`),
  KEY `IX_TranscriptionJobs_UserId` (`UserId`),
  KEY `IX_TranscriptionJobs_Status` (`Status`),
  KEY `IX_TranscriptionJobs_SourceMediaFileId` (`SourceMediaFileId`),
  KEY `IX_TranscriptionJobs_ScoreDbId` (`ScoreDbId`),
  CONSTRAINT `FK_TranscriptionJobs_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_TranscriptionJobs_MediaFiles_SourceMediaFileId`
    FOREIGN KEY (`SourceMediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_TranscriptionJobs_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Compatibility / extension tables for community and personal-center features.
-- Current backend core does not require them, but keeping them does not hurt
-- and helps teammate-owned modules continue to evolve against the same database.

CREATE TABLE IF NOT EXISTS `UserProfiles` (
  `UserId` int NOT NULL,
  `DisplayName` varchar(100) NULL,
  `AvatarMediaFileId` int NULL,
  `Bio` longtext NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`UserId`),
  KEY `IX_UserProfiles_AvatarMediaFileId` (`AvatarMediaFileId`),
  CONSTRAINT `FK_UserProfiles_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_UserProfiles_MediaFiles_AvatarMediaFileId`
    FOREIGN KEY (`AvatarMediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `UserPreferences` (
  `UserId` int NOT NULL,
  `Theme` varchar(50) NOT NULL DEFAULT 'default',
  `DefaultExportFormats` longtext NULL,
  `SyncEnabled` tinyint(1) NOT NULL DEFAULT 1,
  `UpdatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`UserId`),
  CONSTRAINT `FK_UserPreferences_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreCategories` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(100) NOT NULL,
  `Slug` varchar(100) NOT NULL,
  `SortOrder` int NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_ScoreCategories_Name` (`Name`),
  UNIQUE KEY `IX_ScoreCategories_Slug` (`Slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreCategoryRelations` (
  `ScoreDbId` int NOT NULL,
  `CategoryId` int NOT NULL,
  PRIMARY KEY (`ScoreDbId`, `CategoryId`),
  KEY `IX_ScoreCategoryRelations_CategoryId` (`CategoryId`),
  CONSTRAINT `FK_ScoreCategoryRelations_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreCategoryRelations_ScoreCategories_CategoryId`
    FOREIGN KEY (`CategoryId`) REFERENCES `ScoreCategories` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreComments` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScoreDbId` int NOT NULL,
  `UserId` int NOT NULL,
  `Content` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `UpdatedAt` datetime(6) NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'visible',
  PRIMARY KEY (`Id`),
  KEY `IX_ScoreComments_ScoreDbId` (`ScoreDbId`),
  KEY `IX_ScoreComments_UserId` (`UserId`),
  CONSTRAINT `FK_ScoreComments_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreComments_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreFavorites` (
  `UserId` int NOT NULL,
  `ScoreDbId` int NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`UserId`, `ScoreDbId`),
  KEY `IX_ScoreFavorites_ScoreDbId` (`ScoreDbId`),
  CONSTRAINT `FK_ScoreFavorites_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreFavorites_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreDownloads` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScoreDbId` int NOT NULL,
  `UserId` int NULL,
  `SourceIp` varchar(64) NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  KEY `IX_ScoreDownloads_ScoreDbId` (`ScoreDbId`),
  KEY `IX_ScoreDownloads_UserId` (`UserId`),
  CONSTRAINT `FK_ScoreDownloads_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreDownloads_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreOrders` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` int NOT NULL,
  `ScoreDbId` int NOT NULL,
  `AmountCent` int NOT NULL DEFAULT 0,
  `Status` varchar(20) NOT NULL DEFAULT 'pending',
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `PaidAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ScoreOrders_UserId` (`UserId`),
  KEY `IX_ScoreOrders_ScoreDbId` (`ScoreDbId`),
  CONSTRAINT `FK_ScoreOrders_Users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreOrders_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ScoreExports` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScoreDbId` int NOT NULL,
  `MediaFileId` int NOT NULL,
  `ExportType` varchar(20) NOT NULL,
  `CreatedByUserId` int NOT NULL,
  `CreatedAt` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`Id`),
  KEY `IX_ScoreExports_ScoreDbId` (`ScoreDbId`),
  KEY `IX_ScoreExports_MediaFileId` (`MediaFileId`),
  KEY `IX_ScoreExports_CreatedByUserId` (`CreatedByUserId`),
  CONSTRAINT `FK_ScoreExports_Scores_ScoreDbId`
    FOREIGN KEY (`ScoreDbId`) REFERENCES `Scores` (`Id`)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreExports_MediaFiles_MediaFileId`
    FOREIGN KEY (`MediaFileId`) REFERENCES `MediaFiles` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `FK_ScoreExports_Users_CreatedByUserId`
    FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;
