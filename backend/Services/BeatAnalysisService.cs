using System.IO;
using System.Linq;
using backend.Models;

namespace backend.Services;

public sealed class BeatAnalysisService : IBeatAnalysisService
{
    private const double MinimumDurationSeconds = 2.0;
    private const double MinimumBpm = 60.0;
    private const double MaximumBpm = 200.0;

    public BeatAnalysisResult AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return CreateUnavailable("未找到可分析的音频文件。");
        }

        try
        {
            var audioData = WavAudioReader.Read(filePath);
            if (audioData.DurationSeconds < MinimumDurationSeconds)
            {
                return CreateUnavailable("音频时长过短，无法可靠分析节拍。");
            }

            return AnalyzeSamples(audioData.Samples, audioData.SampleRate, audioData.DurationSeconds);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return CreateUnavailable(exception.Message);
        }
    }

    private static BeatAnalysisResult AnalyzeSamples(float[] samples, int sampleRate, double durationSeconds)
    {
        var normalizedSamples = NormalizeSamples(samples);
        var frameSize = Math.Max(512, sampleRate / 50);
        var hopSize = Math.Max(256, frameSize / 2);
        var onsetEnvelope = BuildOnsetEnvelope(normalizedSamples, frameSize, hopSize);

        if (onsetEnvelope.Length < 32)
        {
            return CreateUnavailable("音频片段过短，无法形成稳定的节拍包络。");
        }

        var tempoCandidates = BuildTempoCandidates(onsetEnvelope, sampleRate, hopSize);
        if (tempoCandidates.Count == 0)
        {
            return CreateUnavailable("当前音频未检测到明显的节拍模式。");
        }

        var bestCandidate = tempoCandidates.OrderByDescending(candidate => candidate.Score).First();
        if (bestCandidate.Score <= 0)
        {
            return CreateUnavailable("当前音频未检测到明显的节拍模式。");
        }

        var beatFrames = ExtractBeatFrames(onsetEnvelope, bestCandidate.Lag);
        if (beatFrames.Count < 2)
        {
            return CreateUnavailable("检测到的拍点数量不足，无法输出稳定节拍。");
        }

        var beatTimes = beatFrames
            .Select(frame => Math.Round(frame * hopSize / (double)sampleRate, 3))
            .Where(time => time <= durationSeconds)
            .ToList();

        var tempoBpm = 60.0 * sampleRate / (bestCandidate.Lag * hopSize);
        var stability = CalculateStability(beatTimes);
        var confidence = CalculateConfidence(tempoCandidates, bestCandidate, onsetEnvelope, beatFrames);
        var timeSignature = EstimateTimeSignature(beatFrames, onsetEnvelope);

        return new BeatAnalysisResult
        {
            IsAvailable = true,
            TempoBpm = Math.Round(tempoBpm, 1),
            BeatTimes = beatTimes,
            Stability = Math.Round(stability, 3),
            Confidence = Math.Round(confidence, 3),
            TimeSignatureNumerator = timeSignature.Numerator,
            TimeSignatureDenominator = 4,
            TimeSignatureConfidence = Math.Round(timeSignature.Confidence, 3),
            GridSource = "detected",
            Summary = $"检测到约 {tempoBpm:F1} BPM，节奏稳定度 {stability:P0}，推测为 {timeSignature.Numerator}/4 拍。"
        };
    }

    private static float[] NormalizeSamples(float[] samples)
    {
        if (samples.Length == 0)
        {
            return samples;
        }

        var mean = samples.Average(sample => (double)sample);
        var normalized = new float[samples.Length];
        var peak = 0.0;

        for (var index = 0; index < samples.Length; index++)
        {
            var centered = samples[index] - mean;
            normalized[index] = (float)centered;
            peak = Math.Max(peak, Math.Abs(centered));
        }

        if (peak <= 0)
        {
            return normalized;
        }

        for (var index = 0; index < normalized.Length; index++)
        {
            normalized[index] /= (float)peak;
        }

        return normalized;
    }

    private static double[] BuildOnsetEnvelope(float[] samples, int frameSize, int hopSize)
    {
        var frameCount = 1 + Math.Max(0, (samples.Length - frameSize) / hopSize);
        if (frameCount <= 1)
        {
            return Array.Empty<double>();
        }

        var transientEnergy = new double[frameCount];

        // 用短时差分能量近似 onset envelope，避免直接依赖 FFT。
        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameStart = frame * hopSize;
            var sum = 0.0;
            var previous = frameStart == 0 ? 0.0f : samples[frameStart - 1];

            for (var index = 0; index < frameSize && frameStart + index < samples.Length; index++)
            {
                var current = samples[frameStart + index];
                sum += Math.Abs(current - previous);
                previous = current;
            }

            transientEnergy[frame] = sum / frameSize;
        }

        var envelope = new double[frameCount];
        for (var index = 1; index < frameCount; index++)
        {
            var delta = transientEnergy[index] - transientEnergy[index - 1];
            envelope[index] = delta > 0 ? delta : 0;
        }

        Smooth(envelope, 2);
        ApplyAdaptiveThreshold(envelope, 8, 0.6);
        NormalizeInPlace(envelope);
        return envelope;
    }

    private static List<TempoCandidate> BuildTempoCandidates(double[] onsetEnvelope, int sampleRate, int hopSize)
    {
        var minLag = Math.Max(1, (int)Math.Round(60.0 * sampleRate / (MaximumBpm * hopSize)));
        var maxLag = Math.Min(
            onsetEnvelope.Length - 2,
            (int)Math.Round(60.0 * sampleRate / (MinimumBpm * hopSize)));

        if (maxLag <= minLag)
        {
            return new List<TempoCandidate>();
        }

        var rawScores = new double[maxLag + 1];
        for (var lag = minLag; lag <= maxLag; lag++)
        {
            var score = 0.0;
            for (var index = lag; index < onsetEnvelope.Length; index++)
            {
                score += onsetEnvelope[index] * onsetEnvelope[index - lag];
            }

            rawScores[lag] = score;
        }

        var candidates = new List<TempoCandidate>(maxLag - minLag + 1);
        for (var lag = minLag; lag <= maxLag; lag++)
        {
            var score = rawScores[lag];

            if (lag * 2 <= maxLag)
            {
                score += rawScores[lag * 2] * 0.35;
            }

            if (lag / 2 >= minLag)
            {
                score += rawScores[lag / 2] * 0.15;
            }

            candidates.Add(new TempoCandidate(lag, score));
        }

        return candidates;
    }

    private static List<int> ExtractBeatFrames(double[] onsetEnvelope, int lag)
    {
        var bestOffset = 0;
        var bestOffsetScore = double.MinValue;
        for (var offset = 0; offset < lag; offset++)
        {
            var score = 0.0;
            for (var frame = offset; frame < onsetEnvelope.Length; frame += lag)
            {
                score += onsetEnvelope[frame];
            }

            if (score > bestOffsetScore)
            {
                bestOffsetScore = score;
                bestOffset = offset;
            }
        }

        var threshold = onsetEnvelope.Max() * 0.25;
        var searchRadius = Math.Max(1, lag / 6);
        var beatFrames = new List<int>();

        for (var candidate = bestOffset; candidate < onsetEnvelope.Length; candidate += lag)
        {
            var refined = FindLocalPeak(onsetEnvelope, candidate, searchRadius);
            if (onsetEnvelope[refined] < threshold)
            {
                continue;
            }

            if (beatFrames.Count > 0 && refined - beatFrames[^1] < lag * 0.6)
            {
                continue;
            }

            beatFrames.Add(refined);
        }

        if (beatFrames.Count == 0)
        {
            for (var candidate = bestOffset; candidate < onsetEnvelope.Length; candidate += lag)
            {
                beatFrames.Add(Math.Min(candidate, onsetEnvelope.Length - 1));
            }
        }

        return beatFrames;
    }

    private static int FindLocalPeak(double[] onsetEnvelope, int center, int radius)
    {
        var start = Math.Max(0, center - radius);
        var end = Math.Min(onsetEnvelope.Length - 1, center + radius);
        var bestIndex = center;
        var bestValue = onsetEnvelope[center];

        for (var index = start; index <= end; index++)
        {
            if (onsetEnvelope[index] > bestValue)
            {
                bestValue = onsetEnvelope[index];
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static double CalculateStability(IReadOnlyList<double> beatTimes)
    {
        if (beatTimes.Count < 3)
        {
            return 0;
        }

        var intervals = new double[beatTimes.Count - 1];
        for (var index = 1; index < beatTimes.Count; index++)
        {
            intervals[index - 1] = beatTimes[index] - beatTimes[index - 1];
        }

        var mean = intervals.Average();
        if (mean <= 0)
        {
            return 0;
        }

        var variance = intervals.Select(interval => Math.Pow(interval - mean, 2)).Average();
        var coefficientOfVariation = Math.Sqrt(variance) / mean;
        return Math.Clamp(1.0 - coefficientOfVariation / 0.2, 0.0, 1.0);
    }

    private static double CalculateConfidence(
        IReadOnlyList<TempoCandidate> tempoCandidates,
        TempoCandidate bestCandidate,
        double[] onsetEnvelope,
        IReadOnlyList<int> beatFrames)
    {
        var zeroLagEnergy = onsetEnvelope.Sum(value => value * value);
        var normalizedPeak = zeroLagEnergy <= 0 ? 0 : bestCandidate.Score / zeroLagEnergy;

        var rivalScore = tempoCandidates
            .Where(candidate => Math.Abs(candidate.Lag - bestCandidate.Lag) > 2)
            .Select(candidate => candidate.Score)
            .DefaultIfEmpty(0)
            .Max();

        var dominance = bestCandidate.Score <= 0 ? 0 : (bestCandidate.Score - rivalScore) / bestCandidate.Score;
        var beatStrength = beatFrames.Count == 0
            ? 0
            : beatFrames.Average(frame => onsetEnvelope[Math.Clamp(frame, 0, onsetEnvelope.Length - 1)]);

        return Math.Clamp(normalizedPeak * 0.45 + dominance * 0.35 + beatStrength * 0.20, 0.0, 1.0);
    }

    private static TimeSignatureEstimate EstimateTimeSignature(IReadOnlyList<int> beatFrames, double[] onsetEnvelope)
    {
        if (beatFrames.Count < 8)
        {
            return new TimeSignatureEstimate(4, 0.25);
        }

        var estimates = new[]
        {
            new { Numerator = 2, Contrast = ComputeAccentContrast(beatFrames, onsetEnvelope, 2) },
            new { Numerator = 3, Contrast = ComputeAccentContrast(beatFrames, onsetEnvelope, 3) },
            new { Numerator = 4, Contrast = ComputeAccentContrast(beatFrames, onsetEnvelope, 4) }
        }
            .OrderByDescending(item => item.Contrast)
            .ToArray();

        var best = estimates[0];
        var rival = estimates[1];
        var confidence = best.Contrast <= 0
            ? 0.0
            : Math.Clamp((best.Contrast - rival.Contrast) / Math.Max(best.Contrast, 0.0001), 0.0, 1.0);
        return new TimeSignatureEstimate(best.Numerator, confidence);
    }

    private static double ComputeAccentContrast(IReadOnlyList<int> beatFrames, double[] onsetEnvelope, int groupSize)
    {
        var sums = new double[groupSize];
        var counts = new int[groupSize];

        for (var index = 0; index < beatFrames.Count; index++)
        {
            var bucket = index % groupSize;
            sums[bucket] += onsetEnvelope[Math.Clamp(beatFrames[index], 0, onsetEnvelope.Length - 1)];
            counts[bucket]++;
        }

        var averages = new double[groupSize];
        for (var index = 0; index < groupSize; index++)
        {
            averages[index] = counts[index] == 0 ? 0 : sums[index] / counts[index];
        }

        var mean = averages.Average();
        if (mean <= 0)
        {
            return 0;
        }

        var variance = averages.Select(value => Math.Pow(value - mean, 2)).Average();
        return Math.Sqrt(variance) / mean;
    }

    private static void Smooth(double[] values, int radius)
    {
        if (values.Length == 0 || radius <= 0)
        {
            return;
        }

        var source = values.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var start = Math.Max(0, index - radius);
            var end = Math.Min(values.Length - 1, index + radius);
            var sum = 0.0;

            for (var cursor = start; cursor <= end; cursor++)
            {
                sum += source[cursor];
            }

            values[index] = sum / (end - start + 1);
        }
    }

    private static void ApplyAdaptiveThreshold(double[] values, int radius, double scale)
    {
        var source = values.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var start = Math.Max(0, index - radius);
            var end = Math.Min(values.Length - 1, index + radius);
            var sum = 0.0;

            for (var cursor = start; cursor <= end; cursor++)
            {
                sum += source[cursor];
            }

            var average = sum / (end - start + 1);
            values[index] = Math.Max(0, source[index] - average * scale);
        }
    }

    private static void NormalizeInPlace(double[] values)
    {
        var peak = values.Length == 0 ? 0 : values.Max();
        if (peak <= 0)
        {
            return;
        }

        for (var index = 0; index < values.Length; index++)
        {
            values[index] /= peak;
        }
    }

    private static BeatAnalysisResult CreateUnavailable(string summary)
    {
        return new BeatAnalysisResult
        {
            IsAvailable = false,
            GridSource = "unavailable",
            Summary = summary
        };
    }

    private sealed record TempoCandidate(int Lag, double Score);
    private sealed record TimeSignatureEstimate(int Numerator, double Confidence);
}

internal static class WavAudioReader
{
    public static WavAudioData Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException("当前仅支持 RIFF/WAV 音频。");
        }

        _ = reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException("当前仅支持标准 WAV 音频。");
        }

        ushort formatTag = 0;
        ushort channelCount = 0;
        var sampleRate = 0;
        ushort blockAlign = 0;
        ushort bitsPerSample = 0;
        byte[]? dataChunk = null;

        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();
            var chunkDataStart = reader.BaseStream.Position;

            if (chunkId == "fmt ")
            {
                formatTag = reader.ReadUInt16();
                channelCount = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                _ = reader.ReadInt32();
                blockAlign = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                dataChunk = reader.ReadBytes((int)chunkSize);
            }

            reader.BaseStream.Position = chunkDataStart + chunkSize;
            if ((chunkSize & 1) == 1 && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                reader.BaseStream.Position++;
            }
        }

        if (formatTag == 0 || channelCount == 0 || sampleRate <= 0 || blockAlign == 0 || dataChunk == null)
        {
            throw new InvalidDataException("WAV 文件缺少必要的格式或音频数据块。");
        }

        var monoSamples = DecodeSamples(dataChunk, formatTag, channelCount, blockAlign, bitsPerSample);
        return new WavAudioData(monoSamples, sampleRate, monoSamples.Length / (double)sampleRate);
    }

    private static float[] DecodeSamples(
        byte[] data,
        ushort formatTag,
        ushort channelCount,
        ushort blockAlign,
        ushort bitsPerSample)
    {
        var frameCount = data.Length / blockAlign;
        var bytesPerSample = bitsPerSample / 8;
        var monoSamples = new float[frameCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0.0;
            var frameOffset = frame * blockAlign;

            for (var channel = 0; channel < channelCount; channel++)
            {
                var sampleOffset = frameOffset + channel * bytesPerSample;
                sum += ReadSample(data, sampleOffset, formatTag, bitsPerSample);
            }

            monoSamples[frame] = (float)(sum / channelCount);
        }

        return monoSamples;
    }

    private static double ReadSample(byte[] data, int offset, ushort formatTag, ushort bitsPerSample)
    {
        return formatTag switch
        {
            1 => bitsPerSample switch
            {
                8 => (data[offset] - 128) / 128.0,
                16 => BitConverter.ToInt16(data, offset) / 32768.0,
                24 => Read24BitPcm(data, offset) / 8388608.0,
                32 => BitConverter.ToInt32(data, offset) / 2147483648.0,
                _ => throw new NotSupportedException($"暂不支持 {bitsPerSample}-bit PCM WAV。")
            },
            3 => bitsPerSample switch
            {
                32 => BitConverter.ToSingle(data, offset),
                64 => BitConverter.ToDouble(data, offset),
                _ => throw new NotSupportedException($"暂不支持 {bitsPerSample}-bit float WAV。")
            },
            _ => throw new NotSupportedException("当前仅支持 PCM 或 IEEE float 编码的 WAV。")
        };
    }

    private static int Read24BitPcm(byte[] data, int offset)
    {
        var value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }
}

internal sealed record WavAudioData(float[] Samples, int SampleRate, double DurationSeconds);
