using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json; // 使用项目中已有的Newtonsoft.Json

namespace SeeMusicApp
{
    // 通用 API 响应模型，与后端保持一致
    public class ApiResponse<T>
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }
    }

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

    // 注册响应模型
    public class RegisterResponse
    {
        [JsonProperty("userId")]
        public int UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }

    // 登录响应数据模型
    public class LoginResponse
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }

        [JsonProperty("expiresIn")]
        public int ExpiresIn { get; set; }

        [JsonProperty("user")]
        public UserDto User { get; set; }
    }

    // 用户数据模型
    public class UserDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }
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
            if (!string.IsNullOrEmpty(AccessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            }

            var response = await _httpClient.PostAsync(BaseUrl + endpoint, content);
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
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<ScoreDto>>>(responseBody);
            return apiResponse.Data;
        }

        public async Task<ScoreDetailDto> GetScoreDetailAsync(int scoreId)
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}community/scores/{scoreId}");
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<ScoreDetailDto>>(responseBody);
            return apiResponse.Data;
        }
    }

    public class ScoreDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string AuthorName { get; set; }
        public string CoverUrl { get; set; }
        public int Price { get; set; }
        public int DownloadCount { get; set; }
    }

    public class ScoreDetailDto : ScoreDto
    {
        public string Description { get; set; }
        public string FileUrl { get; set; }
    }
}
