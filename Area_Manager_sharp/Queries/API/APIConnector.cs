using System.Text.Json;
using Area_Manager_sharp.Queries.API.Deserialization;
using System.Text;

namespace Area_Manager_sharp.Queries.API
{
	internal class APIConnector: IDisposable
	{
		private static readonly HttpClientHandler _handler = new HttpClientHandler
		{
			UseCookies = true,
		};
		private static readonly HttpClient _httpClient;
		public static HttpClient Client
		{
			get
			{
				return _httpClient;
			}
		}

		private static readonly string _baseUrl;
		private static readonly string _login;
		private static readonly string _password;

		static APIConnector()
		{
			_httpClient = new HttpClient(_handler);
			// Настройка для работы с куками (важно для сессий)
			//_httpClient.DefaultRequestHeaders.Add("Cookie", "");

			_baseUrl = Program.API_URL_CONNECTION;
			_login = Program.API_LOGIN_CONNECTION;
			_password = Program.API_PASSWORD_CONNECTION;
		}

		public static async Task<bool> CheckAuthAsync()
		{
			var response = await _httpClient.GetAsync($"{_baseUrl}/api/check-auth");
			return response.IsSuccessStatusCode; // 200 = авторизован, 401 = нет
		}

		public static async Task<(bool Success, int? UserId, string Error)> LoginAsync()
		{
			var content = new StringContent(
				JsonSerializer.Serialize(new { login_user = _login, password_user = _password }),
				Encoding.UTF8,
				"application/json");

			var response = await _httpClient.PostAsync($"{_baseUrl}/api/login", content);

			if (response.IsSuccessStatusCode)
			{
				var responseContent = await response.Content.ReadAsStringAsync();
				var result = JsonSerializer.Deserialize<LoginResponse>(responseContent);
				return (true, result?.user_id, string.Empty);
			}
			else
			{
				var errorContent = await response.Content.ReadAsStringAsync();
				return (false, null, errorContent);
			}
		}

		public static async Task<bool> LogoutAsync()
		{
			var response = await _httpClient.GetAsync($"{_baseUrl}/api/logout");
			return response.IsSuccessStatusCode;
		}

		public async void Dispose()
		{
			await LogoutAsync();
		}
	}
}
