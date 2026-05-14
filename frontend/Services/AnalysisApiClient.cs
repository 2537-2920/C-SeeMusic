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
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

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

        public async Task<EvaluationWorkflowResult> SubmitEvaluationAsync(
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

        public async Task<EvaluationWorkflowResult> SubmitSingingEvaluationAsync(SingingEvaluationRequest request)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.PerformanceFilePath)
                || !File.Exists(request.PerformanceFilePath))
            {
                throw new InvalidOperationException("请先选择可用的演唱音频文件。");
            }

            var submit = await SubmitSingingEvaluationRequestAsync(request);

            var status = new EvaluationStatusResponse
            {
                EvaluationId = submit.EvaluationId,
                Status = submit.Status,
                Progress = submit.Progress,
                Warnings = submit.Warnings ?? new System.Collections.Generic.List<string>()
            };

            if (submit.ReportPreview != null && string.Equals(submit.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                return new EvaluationWorkflowResult
                {
                    Submit = submit,
                    Status = status,
                    Report = submit.ReportPreview
                };
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (string.Equals(status.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1.5));
                status = await GetSingingEvaluationStatusAsync(submit.EvaluationId, submit.AnonymousAccessToken);
            }

            if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var message = !string.IsNullOrWhiteSpace(status.ErrorMessage)
                    ? status.ErrorMessage
                    : (status.Warnings != null && status.Warnings.Count > 0 ? status.Warnings[0] : "评估失败。");
                throw new InvalidOperationException(message);
            }

            if (!string.Equals(status.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("评估任务仍在处理中，请稍后重试。");
            }

            var report = await GetSingingEvaluationReportAsync(submit.EvaluationId, submit.AnonymousAccessToken);
            return new EvaluationWorkflowResult
            {
                Submit = submit,
                Status = status,
                Report = report
            };
        }

        public async Task<TransposeSuggestionResponse> GetTransposeSuggestionAsync(
            string evaluationId,
            string anonymousAccessToken,
            string sourceGender,
            string targetGender)
        {
            var json = _serializer.Serialize(new TransposeSuggestionRequest
            {
                SourceGender = sourceGender,
                TargetGender = targetGender
            });

            var url = BuildUrl("/api/v1/singing/evaluate/" + Uri.EscapeDataString(evaluationId) + "/transpose-suggestion");
            if (!string.IsNullOrWhiteSpace(anonymousAccessToken))
            {
                url += "?accessToken=" + Uri.EscapeDataString(anonymousAccessToken);
            }

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await HttpClient.PostAsync(url, content);
                var payload = await ReadApiResponseAsync<TransposeSuggestionResponse>(response);
                return payload.Data;
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

            return "http://localhost:5000";
        }

        private async Task<MediaUploadResponse> UploadAudioAsync(string filePath)
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

        private async Task<EvaluationSubmitResponse> SubmitSingingEvaluationRequestAsync(SingingEvaluationRequest request)
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
                    if (!string.IsNullOrWhiteSpace(request.ReferenceFilePath) && File.Exists(request.ReferenceFilePath))
                    {
                        referenceStream = File.OpenRead(request.ReferenceFilePath);
                        referenceContent = new StreamContent(referenceStream);
                        referenceContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(request.ReferenceFilePath));
                        form.Add(referenceContent, "referenceFile", Path.GetFileName(request.ReferenceFilePath));
                    }

                    var response = await HttpClient.PostAsync(BuildUrl("/api/v1/singing/evaluate"), form);
                    var payload = await ReadApiResponseAsync<EvaluationSubmitResponse>(response);
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

        private async Task<EvaluationStatusResponse> GetSingingEvaluationStatusAsync(string evaluationId, string accessToken)
        {
            var url = BuildUrl("/api/v1/singing/evaluate/" + Uri.EscapeDataString(evaluationId));
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                url += "?accessToken=" + Uri.EscapeDataString(accessToken);
            }

            var response = await HttpClient.GetAsync(url);
            var payload = await ReadApiResponseAsync<EvaluationStatusResponse>(response);
            return payload.Data;
        }

        private async Task<EvaluationReportResponse> GetSingingEvaluationReportAsync(string evaluationId, string accessToken)
        {
            var url = BuildUrl("/api/v1/singing/evaluate/" + Uri.EscapeDataString(evaluationId) + "/report");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                url += "?accessToken=" + Uri.EscapeDataString(accessToken);
            }

            var response = await HttpClient.GetAsync(url);
            var payload = await ReadApiResponseAsync<EvaluationReportResponse>(response);
            return payload.Data;
        }

        private async Task<ApiResponse<T>> ReadApiResponseAsync<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            ApiResponse<T> payload = null;

            if (!string.IsNullOrWhiteSpace(body))
            {
                payload = _serializer.Deserialize<ApiResponse<T>>(body);
            }

            if (payload == null)
            {
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

        private string BuildUrl(string relativePath)
        {
            return GetBackendBaseUrl() + relativePath;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
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
