using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Diagnostics;

namespace backend.Services;

public sealed class AudioPreparationService : IAudioPreparationService
{
    private readonly SeeMusicDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public AudioPreparationService(SeeMusicDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    public async Task<PreparedAudioResult> PrepareAsync(MediaFile mediaFile, CancellationToken cancellationToken = default)
    {
        if (string.Equals(mediaFile.PreparedAudioStatus, "ready", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(mediaFile.PreparedAudioPath))
        {
            var preparedAbsolutePath = BuildAbsolutePath(mediaFile.PreparedAudioPath);
            if (File.Exists(preparedAbsolutePath))
            {
                return new PreparedAudioResult
                {
                    Status = "ready",
                    AbsolutePath = preparedAbsolutePath,
                    DurationMs = mediaFile.DurationMs,
                };
            }
        }

        var absoluteSourcePath = BuildAbsolutePath(mediaFile.StoragePath);
        if (!File.Exists(absoluteSourcePath))
        {
            mediaFile.PreparedAudioStatus = "failed";
            mediaFile.PreparationErrorMessage = "源音频文件不存在，无法准备评估素材。";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new PreparedAudioResult
            {
                Status = "failed",
                ErrorMessage = mediaFile.PreparationErrorMessage,
            };
        }

        if (!IsWaveFile(mediaFile))
        {
            return await PrepareWithFfmpegAsync(mediaFile, absoluteSourcePath, cancellationToken);
        }

        try
        {
            var audioData = WavAudioReader.Read(absoluteSourcePath);
            mediaFile.DurationMs = (int)Math.Round(audioData.DurationSeconds * 1000);
            mediaFile.PreparedAudioStatus = "ready";
            mediaFile.PreparedAudioPath = mediaFile.StoragePath;
            mediaFile.PreparationErrorMessage = null;
            _dbContext.MediaFiles.Update(mediaFile);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PreparedAudioResult
            {
                Status = "ready",
                AbsolutePath = absoluteSourcePath,
                DurationMs = mediaFile.DurationMs,
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            mediaFile.PreparedAudioStatus = "failed";
            mediaFile.PreparationErrorMessage = exception.Message;
            _dbContext.MediaFiles.Update(mediaFile);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PreparedAudioResult
            {
                Status = "failed",
                ErrorMessage = exception.Message,
            };
        }
    }

    private async Task<PreparedAudioResult> PrepareWithFfmpegAsync(
        MediaFile mediaFile,
        string absoluteSourcePath,
        CancellationToken cancellationToken)
    {
        var preparedDirectory = Path.Combine(_environment.ContentRootPath, "uploads", "prepared");
        Directory.CreateDirectory(preparedDirectory);
        var preparedFileName = $"{mediaFile.MediaId}.wav";
        var preparedAbsolutePath = Path.Combine(preparedDirectory, preparedFileName);
        var preparedRelativePath = Path.Combine("prepared", preparedFileName).Replace('\\', '/');

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{absoluteSourcePath}\" -ac 1 -ar 44100 -sample_fmt s16 \"{preparedAbsolutePath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 ffmpeg 进行音频转码。");
            }

            _ = process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || !File.Exists(preparedAbsolutePath))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput)
                    ? "ffmpeg 转码失败，无法生成标准 WAV 素材。"
                    : $"ffmpeg 转码失败：{errorOutput.Trim()}");
            }

            var audioData = WavAudioReader.Read(preparedAbsolutePath);
            mediaFile.DurationMs = (int)Math.Round(audioData.DurationSeconds * 1000);
            mediaFile.PreparedAudioStatus = "ready";
            mediaFile.PreparedAudioPath = preparedRelativePath;
            mediaFile.PreparationErrorMessage = null;
            _dbContext.MediaFiles.Update(mediaFile);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PreparedAudioResult
            {
                Status = "ready",
                AbsolutePath = preparedAbsolutePath,
                DurationMs = mediaFile.DurationMs,
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or InvalidDataException or NotSupportedException)
        {
            mediaFile.PreparedAudioStatus = "failed";
            mediaFile.PreparationErrorMessage = exception is Win32Exception
                ? "当前环境未找到 ffmpeg，无法自动转码非 WAV 素材。"
                : exception.Message;
            _dbContext.MediaFiles.Update(mediaFile);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PreparedAudioResult
            {
                Status = "failed",
                ErrorMessage = mediaFile.PreparationErrorMessage,
            };
        }
    }

    private string BuildAbsolutePath(string relativePath)
    {
        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.ContentRootPath, "uploads", normalized);
    }

    private static bool IsWaveFile(MediaFile mediaFile)
    {
        var extension = Path.GetExtension(mediaFile.FileName);
        return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaFile.MimeType, "audio/wav", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaFile.MimeType, "audio/x-wav", StringComparison.OrdinalIgnoreCase);
    }
}
