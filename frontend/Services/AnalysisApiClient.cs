using SeeMusicApp.Models;
using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SeeMusicApp.Services
{
    public sealed class AnalysisApiClient
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue
        };

        public async Task<AnalysisWorkflowResult> AnalyzeAudioAsync(
            string filePath,
            bool separateMelody,
            bool separateAccompaniment)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new InvalidOperationException("请先选择可用的本地音频文件。");
            }

            var upload = await UploadAudioAsync(filePath);
            var analysis = await RequestAnalysisAsync(new TranscriptionRequest
            {
                MediaId = upload.MediaId,
                SeparateMelody = separateMelody,
                SeparateAccompaniment = separateAccompaniment
            });

            return new AnalysisWorkflowResult
            {
                Upload = upload,
                Analysis = analysis
            };
        }

        public async Task<HealthStatusResponse> GetHealthAsync()
        {
            var response = await HttpClient.GetAsync(BuildUrl("/health"));
            var payload = await ReadApiResponseAsync<HealthStatusResponse>(response);
            return payload.Data;
        }

        public async Task<CreateTranscriptionResponse> CreatePianoTranscriptionAsync(string mediaId, string projectTitle)
        {
            var json = _serializer.Serialize(new CreateTranscriptionRequest
            {
                SourceType = "audio",
                MediaId = mediaId,
                ProjectTitle = projectTitle,
                Options = new TranscriptionOptionsRequest
                {
                    Mode = "piano",
                    SeparateMelody = true,
                    SeparateAccompaniment = true,
                    AnalyzeRhythm = true
                }
            });

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await HttpClient.PostAsync(BuildUrl("/api/v1/transcriptions"), content);
                var payload = await ReadApiResponseAsync<CreateTranscriptionResponse>(response);
                return payload.Data;
            }
        }

        public async Task<TranscriptionStatusResponse> GetTranscriptionStatusAsync(string jobId)
        {
            var response = await HttpClient.GetAsync(BuildUrl("/api/v1/transcriptions/" + Uri.EscapeDataString(jobId)));
            var payload = await ReadApiResponseAsync<TranscriptionStatusResponse>(response);
            return payload.Data;
        }

        public async Task<ScoreDetailResponse> GetScoreAsync(string scoreId)
        {
            var response = await HttpClient.GetAsync(BuildUrl("/api/v1/scores/" + Uri.EscapeDataString(scoreId)));
            var payload = await ReadApiResponseAsync<ScoreDetailResponse>(response);
            return payload.Data;
        }

        public async Task<EvaluationReportResponse> SubmitEvaluationAsync(
            string performanceFilePath,
            string referenceFilePath,
            bool analyzePitch,
            bool analyzeRhythm)
        {
            return await SubmitSingingEvaluationAsync(new SingingEvaluationRequest
            {
                PerformanceFilePath = performanceFilePath,
                ReferenceFilePath = referenceFilePath,
                AnalyzePitch = analyzePitch,
                AnalyzeRhythm = analyzeRhythm,
                UserAudioType = "with_accompaniment",
                FeedbackLanguage = "zh-CN",
                ScoringModel = "balanced",
                RhythmThresholdMs = 50
            });
        }

        public async Task<EvaluationReportResponse> SubmitSingingEvaluationAsync(SingingEvaluationRequest request)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.PerformanceFilePath)
                || !File.Exists(request.PerformanceFilePath))
            {
                throw new InvalidOperationException("请先选择可用的演唱音频文件。");
            }

            if (string.IsNullOrWhiteSpace(request.ReferenceFilePath)
                || !File.Exists(request.ReferenceFilePath))
            {
                throw new InvalidOperationException("请先选择可用的标准音频文件。");
            }

            return await SubmitSingingEvaluationRequestAsync(request);
        }

        public async Task<ScoreDetailResponse> TranscribeInstantAsync(string filePath, string title)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                throw new InvalidOperationException("请先选择可用的本地音频文件。");
            }

            using (var form = new MultipartFormDataContent())
            using (var stream = File.OpenRead(filePath))
            using (var fileContent = new StreamContent(stream))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(filePath));
                form.Add(fileContent, "audioFile", Path.GetFileName(filePath));
                form.Add(new StringContent(string.IsNullOrWhiteSpace(title) ? string.Empty : title), "title");
                form.Add(new StringContent("true"), "separateMelody");
                form.Add(new StringContent("true"), "separateAccompaniment");
                form.Add(new StringContent("true"), "analyzeRhythm");

                var response = await HttpClient.PostAsync(BuildUrl("/api/v1/transcriptions/instant"), form);
                var payload = await ReadApiResponseAsync<ScoreDetailResponse>(response);
                return payload.Data;
            }
        }

        public async Task<TransposeSuggestionResponse> GetTransposeSuggestionAsync(
            TransposeBase transposeBase,
            string feedbackLanguage,
            string sourceGender,
            string targetGender)
        {
            var json = _serializer.Serialize(new TransposeSuggestionRequest
            {
                TransposeBase = transposeBase,
                FeedbackLanguage = string.IsNullOrWhiteSpace(feedbackLanguage) ? "zh-CN" : feedbackLanguage,
                SourceGender = sourceGender,
                TargetGender = targetGender
            });

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await HttpClient.PostAsync(BuildUrl("/api/v1/singing/evaluate/transpose-suggestion"), content);
                var payload = await ReadApiResponseAsync<TransposeSuggestionResponse>(response);
                return payload.Data;
            }
        }

        public async Task<byte[]> ExportSingingEvaluationPdfAsync(EvaluationReportResponse report)
        {
            if (report == null || report.Summary == null)
            {
                throw new InvalidOperationException("当前没有可导出的评估报告。");
            }

            var json = _serializer.Serialize(new EvaluationPdfExportRequest
            {
                Report = report
            });

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await HttpClient.PostAsync(BuildUrl("/api/v1/singing/evaluate/export-pdf"), content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(await ReadErrorMessageAsync(response));
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public string GetSingingEvaluatePath()
        {
            return "/api/v1/singing/evaluate";
        }

        public string GetBackendBaseUrl()
        {
            var configured = ConfigurationManager.AppSettings["BackendBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.TrimEnd('/');
            }

            return "http://localhost:5001";
        }

        public async Task<MediaUploadResponse> UploadAudioAsync(string filePath)
        {
            using (var form = new MultipartFormDataContent())
            using (var stream = File.OpenRead(filePath))
            using (var fileContent = new StreamContent(stream))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(filePath));
                form.Add(fileContent, "file", Path.GetFileName(filePath));
                form.Add(new StringContent("audio"), "type");

                var response = await HttpClient.PostAsync(BuildUrl("/api/v1/media/upload"), form);
                var payload = await ReadApiResponseAsync<MediaUploadResponse>(response);
                return payload.Data;
            }
        }

        private async Task<TranscriptionResult> RequestAnalysisAsync(TranscriptionRequest request)
        {
            var json = _serializer.Serialize(request);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await HttpClient.PostAsync(BuildUrl("/api/v1/transcriptions/analyze"), content);
                var payload = await ReadApiResponseAsync<TranscriptionResult>(response);
                return payload.Data;
            }
        }

        private async Task<EvaluationReportResponse> SubmitSingingEvaluationRequestAsync(SingingEvaluationRequest request)
        {
            using (var form = new MultipartFormDataContent())
            using (var performanceStream = File.OpenRead(request.PerformanceFilePath))
            using (var performanceContent = new StreamContent(performanceStream))
            {
                performanceContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(request.PerformanceFilePath));
                form.Add(performanceContent, "performanceFile", Path.GetFileName(request.PerformanceFilePath));
                form.Add(new StringContent(request.AnalyzePitch ? "true" : "false"), "analyzePitch");
                form.Add(new StringContent(request.AnalyzeRhythm ? "true" : "false"), "analyzeRhythm");
                form.Add(new StringContent(string.IsNullOrWhiteSpace(request.UserAudioType) ? "with_accompaniment" : request.UserAudioType), "userAudioType");
                form.Add(new StringContent(string.IsNullOrWhiteSpace(request.FeedbackLanguage) ? "zh-CN" : request.FeedbackLanguage), "feedbackLanguage");
                form.Add(new StringContent(string.IsNullOrWhiteSpace(request.ScoringModel) ? "balanced" : request.ScoringModel), "scoringModel");
                form.Add(new StringContent(request.RhythmThresholdMs.ToString()), "rhythmThresholdMs");

                Stream referenceStream = null;
                StreamContent referenceContent = null;
                try
                {
                    referenceStream = File.OpenRead(request.ReferenceFilePath);
                    referenceContent = new StreamContent(referenceStream);
                    referenceContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(request.ReferenceFilePath));
                    form.Add(referenceContent, "referenceFile", Path.GetFileName(request.ReferenceFilePath));

                    var response = await HttpClient.PostAsync(BuildUrl("/api/v1/singing/evaluate"), form);
                    var payload = await ReadApiResponseAsync<EvaluationReportResponse>(response);
                    return payload.Data;
                }
                finally
                {
                    if (referenceContent != null)
                    {
                        referenceContent.Dispose();
                    }

                    if (referenceStream != null)
                    {
                        referenceStream.Dispose();
                    }
                }
            }
        }

        private async Task<ApiResponse<T>> ReadApiResponseAsync<T>(HttpResponseMessage response)
        {
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                ApiResponse<T> payload = null;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        payload = _serializer.Deserialize<ApiResponse<T>>(body);
                    }
                    catch (Exception)
                    {
                        // Response body is not valid JSON
                    }
                }

                if (payload == null)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException(
                            string.Format("请求失败（HTTP {0}），服务器返回了无效响应。请确认后端服务是否正常运行，以及数据库是否已配置。", (int)response.StatusCode));
                    }

                    throw new InvalidOperationException("后端返回了空响应。");
                }

                if (!response.IsSuccessStatusCode || payload.Code != 0 || payload.Data == null)
                {
                    var message = string.IsNullOrWhiteSpace(payload.Message)
                        ? string.Format("请求失败，HTTP {0}", (int)response.StatusCode)
                        : payload.Message;
                    throw new InvalidOperationException(message);
                }

                return payload;
            }
        }

        private async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var payload = _serializer.Deserialize<ApiResponse<object>>(body);
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.Message))
                    {
                        return payload.Message;
                    }
                }
                catch (Exception)
                {
                }
            }

            return string.Format("请求失败，HTTP {0}", (int)response.StatusCode);
        }

        private string BuildUrl(string relativePath)
        {
            return GetBackendBaseUrl() + relativePath;
        }

        private static HttpClient CreateHttpClient()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 10;
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            return client;
        }

        private static string GetMimeType(string filePath)
        {
            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".wav":
                    return "audio/wav";
                case ".mp3":
                    return "audio/mpeg";
                case ".m4a":
                    return "audio/mp4";
                case ".ogg":
                    return "audio/ogg";
                case ".mp4":
                    return "video/mp4";
                case ".mov":
                    return "video/quicktime";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
