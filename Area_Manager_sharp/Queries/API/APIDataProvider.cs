using Area_Manager_sharp.DBTools.DataProvider;
using Area_Manager_sharp.Queries.API.Deserialization;
using System.Text;
using System.Text.Json;

namespace Area_Manager_sharp.Queries.API
{
	internal class APIDataProvider : IDataProvider
	{
		static APIDataProvider()
		{

		}

		public async Task<object?[,]> ExecuteSelectTableAsync(string sql, CancellationToken cancellationToken = default)
		{
			var result = await ExecuteQueryInternalAsync(sql, null, cancellationToken);

			if (result.Type != "select")
				throw new Exception("API did not return a SELECT result");

			if (result.Data == null || result.Data.Count == 0)
				return new object?[0, 0];

			int rowCount = result.Data.Count;
			int colCount = result.Data[0].Count;
			var arr = new object?[rowCount, colCount];

			for (int i = 0; i < rowCount; i++)
			{
				for (int j = 0; j < colCount; j++)
					arr[i, j] = result.Data[i][j];
			}

			return arr;
		}

		public async Task<object?> ExecuteAnySqlScalarAsync(string sql, CancellationToken cancellationToken = default)
		{
			var result = await ExecuteQueryInternalAsync(sql, null, cancellationToken);

			if (result.Type != "select")
				throw new Exception("API did not return a SELECT result");

			if (result.Data != null && result.Data.Count > 0 && result.Data[0].Count > 0)
				return result.Data[0][0];

			return null;
		}

		public async Task ExecuteUpdateAsync(string table, string value, string conditions, CancellationToken cancellationToken = default)
		{
			if (value.EndsWith(";"))
				value = value[..^1]; // убираем последний символ
			string strValues = value.Replace(';', ',');

			string sql = $"UPDATE {table} SET {strValues} {conditions};";
			await ExecuteNonQueryAsync(sql, cancellationToken);
		}

		public async Task ExecuteDeleteAsync(string table, string conditions, CancellationToken cancellationToken = default)
		{
			string sql = $"DELETE FROM {table} {conditions};";
			await ExecuteNonQueryAsync(sql, cancellationToken);
		}

		public async Task ExecuteInsertAsync(string table, string value, CancellationToken cancellationToken = default)
		{
			if (value.EndsWith(";"))
				value = value[..^1]; // убираем последний символ

			string[] valueMass = value.Split(';');
			string columns = string.Join(", ", valueMass.Select(v => v.Split('=')[0]));
			string values = string.Join(", ", valueMass.Select(v => v.Split('=')[1]));

			string sql = $"INSERT INTO {table} ({columns}) VALUES ({values});";

			await ExecuteNonQueryAsync(sql, cancellationToken);
		}

		// ----------------------------- helpers -----------------------------

		private async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
		{
			var result = await ExecuteQueryInternalAsync(sql, null, cancellationToken);
			if (result.Type != "modify")
				throw new Exception("API did not return a MODIFY result");
		}

		public async Task<int> GetCountRows(string table, string conditions, CancellationToken cancellationToken = default)
		{
			int count = -1;
			string sql = $"select count(*) from {table} {conditions};";
			var result = await ExecuteQueryInternalAsync(sql, null, cancellationToken);

			if (result.Type != "select")
				throw new Exception("API did not return a SELECT result");

			if (result.Data != null && result.Data.Count > 0 && result.Data[0].Count > 0)
				count = Convert.ToInt32(result.Data[0][0]);

			return count;
		}

		private async Task<APIQueryResult> ExecuteQueryInternalAsync(string sql, object[]? parameters, CancellationToken cancellationToken = default)
		{
			if (!await APIConnector.CheckAuthAsync())
				await APIConnector.LoginAsync();

			var requestData = new
			{
				sql,
				args = parameters ?? Array.Empty<object>()
			};

			var content = new StringContent(
				JsonSerializer.Serialize(requestData),
				Encoding.UTF8,
				"application/json");

			var response = await APIConnector.Client.PostAsync($"{Program.API_URL_CONNECTION}/api/execute-query", content, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
				throw new Exception($"API error: {errorContent}");
			}

			var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
			var result = JsonSerializer.Deserialize<APIQueryResult>(responseContent);

			if (result == null)
				throw new Exception("Invalid API response");

			return result;
		}
	}
}

