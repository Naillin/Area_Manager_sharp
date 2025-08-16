using System.Text.Json;
using System.Text.Json.Serialization;

namespace Area_Manager_sharp.Queries.API.Deserialization
{
	internal class APIQueryResult
	{
		[JsonPropertyName("type")]
		public string Type { get; set; } = "";

		[JsonPropertyName("data")]
		public List<List<object?>>? RawData { get; set; }

		[JsonPropertyName("columns")]
		public List<string>? Columns { get; set; }

		[JsonPropertyName("row_count")]
		public int RowCount { get; set; }

		[JsonPropertyName("last_id")]
		public long? LastId { get; set; }

		// Автоматическая конвертация JsonElement в int/string/etc
		[JsonIgnore]
		public List<List<object?>>? Data => RawData?.Select(
			row => row.Select(val => val switch
			{
				JsonElement je when je.ValueKind == JsonValueKind.Number => je.TryGetInt64(out var l) ? (object)l : je.GetDouble(),
				JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
				JsonElement je when je.ValueKind == JsonValueKind.True => true,
				JsonElement je when je.ValueKind == JsonValueKind.False => false,
				JsonElement je when je.ValueKind == JsonValueKind.Null => null,
				JsonElement je => je.GetRawText(),
				_ => val
			}).ToList()
		).ToList();
	}
}
