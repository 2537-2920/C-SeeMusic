using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // 使用项目中已有的Newtonsoft.Json

namespace SeeMusicApp
{
    // 登录请求模型
    public class LoginRequest
    {
        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }

    // 注册请求模型
    public class RegisterRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("confirmPassword")]
        public string ConfirmPassword { get; set; }
    }

    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "http://localhost:5000/api/v1/"; // 后端 API 的基础地址

        public ApiClient()
        {
            _httpClient = new HttpClient();
            // 可以在这里设置一些默认的请求头，例如认证Token
            // _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "your_jwt_token");
        }

        public async Task<LoginResponse> LoginAsync(string account, string password)
        {
            var loginRequest = new LoginRequest { Account = account, Password = password };
            var jsonContent = JsonConvert.SerializeObject(loginRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(BaseUrl + "auth/login", content);
                response.EnsureSuccessStatusCode(); // 确保响应状态码是 2xx 成功

                var responseBody = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<LoginResponse>>(responseBody);

                if (apiResponse.Code == 0)
                {
                    return apiResponse.Data;
                }
                else
                {
                    throw new Exception(apiResponse.Message); // 处理API返回的业务错误
                }
            }
            catch (HttpRequestException ex)
            { 
                // 处理网络请求错误，例如服务器未启动或网络不通
                throw new Exception($"网络请求失败: {ex.Message}", ex);
            }
            catch (Exception ex)
            { 
                // 处理其他异常，例如JSON反序列化失败
                throw new Exception($"登录失败: {ex.Message}", ex);
            }
        }

        public async Task<RegisterResponse> RegisterAsync(string username, string email, string password, string confirmPassword)
        {
            var registerRequest = new RegisterRequest 
            { 
                Username = username, 
                Email = email, 
                Password = password, 
                ConfirmPassword = confirmPassword 
            };
            var jsonContent = JsonConvert.SerializeObject(registerRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(BaseUrl + "auth/register", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<RegisterResponse>>(responseBody);

            if (apiResponse.Code == 0)
            {
                return apiResponse.Data;
            }
            else
            {
                throw new Exception(apiResponse.Message);
            }
        }

        public static string AccessToken { get; set; } // 存储登录后的 Token

        public async Task<T> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + endpoint);
            request.Content = content;

            if (!string.IsNullOrEmpty(AccessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"服务器返回错误: {response.StatusCode} - {responseBody}");
            }

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(responseBody);
            return apiResponse.Data;
        }

        public async Task<List<ScoreDto>> GetScoresAsync(string keyword = null, string category = null)
        {
            var url = $"{BaseUrl}community/scores?keyword={keyword}&category={category}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(AccessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<ScoreDto>>>(responseBody);
            return apiResponse.Data;
        }

        public async Task<ScoreDetailDto> GetScoreDetailAsync(int scoreId)
        {
            var url = $"{BaseUrl}community/scores/{scoreId}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrEmpty(AccessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<ScoreDetailDto>>(responseBody);
            return apiResponse.Data;
        }

        public async Task<bool> AddCommentAsync(int scoreId, string content)
        {
            var url = $"{BaseUrl}community/scores/{scoreId}/comments";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            var commentReq = new CommentRequest { Content = content };
            var json = JsonConvert.SerializeObject(commentReq);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(AccessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ToggleFavoriteAsync(int scoreId, bool isFavorite)
        {
            var url = $"{BaseUrl}community/scores/{scoreId}/favorite";
            HttpRequestMessage request;
            
            if (isFavorite)
            {
                request = new HttpRequestMessage(HttpMethod.Post, url);
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Delete, url);
            }

            if (!string.IsNullOrEmpty(AccessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<byte[]> DownloadScoreAsync(int scoreId)
        {
            // 1. 获取下载路径
            var url = $"{BaseUrl}community/scores/{scoreId}/download";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            if (!string.IsNullOrEmpty(AccessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("获取下载链接失败，请确认是否已登录。");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<string>>(responseBody);
            
            if (string.IsNullOrEmpty(apiResponse.Data))
            {
                throw new Exception("获取下载链接失败。");
            }

            // 2. 下载真实文件
            // 注意：apiResponse.Data 可能是 "/uploads/scores/xxx.pdf"，需要拼接 BaseUrl
            // 拼接时要注意 BaseUrl 是否自带 api/v1/，我们可能需要去掉它
            var fileUrl = apiResponse.Data;
            if (fileUrl.StartsWith("/")) fileUrl = fileUrl.Substring(1);
            
            // 这里假设 BaseUrl 是 http://localhost:5000/api/v1/，
            // 真实的静态文件地址在 http://localhost:5000/uploads/...
            var hostUrl = BaseUrl.Replace("/api/v1/", "/");
            var fullUrl = hostUrl + fileUrl;

            var fileBytes = await _httpClient.GetByteArrayAsync(fullUrl);
            return fileBytes;
        }

        public async Task<Dictionary<string, int>> GetCategoryStatsAsync()
        {
            var url = $"{BaseUrl}community/categories/stats";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<Dictionary<string, int>>>(responseBody);
            return apiResponse.Data;
        }
    }

    public class CommentRequest
    {
        public string Content { get; set; }
    }

    public class ScoreDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("authorName")]
        public string AuthorName { get; set; }

        [JsonProperty("arrangementTag")]
        public string ArrangementTag { get; set; }

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; }

        [JsonProperty("price")]
        public int Price { get; set; }

        [JsonProperty("downloadCount")]
        public int DownloadCount { get; set; }

        [JsonProperty("favoriteCount")]
        public int FavoriteCount { get; set; }

        [JsonProperty("categoryName")]
        public string CategoryName { get; set; }

        [JsonProperty("uploaderName")]
        public string UploaderName { get; set; }
    }

    public class ScoreDetailDto : ScoreDto
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("fileUrl")]
        public string FileUrl { get; set; }

        [JsonProperty("commentCount")]
        public int CommentCount { get; set; }

        [JsonProperty("isFavorited")]
        public bool IsFavorited { get; set; }

        [JsonProperty("recentComments")]
        public List<CommentDto> RecentComments { get; set; }
    }

    public class CommentDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string AvatarUrl { get; set; }
        public string Bio { get; set; }
        public int TranscriptionCount { get; set; }
        public int EvaluationDurationHours { get; set; }
        public int FavoriteCount { get; set; }
    }

    public class DashboardResponse
    {
        public DashboardProfile Profile { get; set; } = new DashboardProfile();
        public DashboardStats Stats { get; set; } = new DashboardStats();
        public List<WeeklyUsageItem> WeeklyUsage { get; set; } = new List<WeeklyUsageItem>();
        public string DebugInfo { get; set; } = "";
    }

    public class DashboardProfile
    {
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string AvatarUrl { get; set; }
        public string Bio { get; set; } = "";
    }

    public class DashboardStats
    {
        public int TranscriptionCount { get; set; }
        public int EvaluationDurationHours { get; set; }
        public int FavoriteCount { get; set; }
    }

    public class WeeklyUsageItem
    {
        public string Day { get; set; } = "";
        public int Value { get; set; }
    }

    public class ApiResponse<T>
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public UserDto User { get; set; }
    }

    public class RegisterResponse
    {
        public string Message { get; set; }
        public UserDto User { get; set; }
    }

    public class UserPreferencesDto
    {
        public string Theme { get; set; } = "light-music";
        public List<string> DefaultExportFormats { get; set; } = new List<string>();
        public bool SyncPreferences { get; set; } = true;
    }
}
