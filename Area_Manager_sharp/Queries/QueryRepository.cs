using Area_Manager_sharp.DBTools.DataProvider;
using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using System.Text.Json;

namespace Area_Manager_sharp.Queries
{
	internal class QueryRepository : IQueryRepository
	{
		IDataProvider _dataProvider;
		public QueryRepository(IDataProvider dataProvider)
		{
			_dataProvider = dataProvider;
		}

		public async Task<object?[,]> GetTopicsAsync() => await _dataProvider.ExecuteSelectTableAsync("SELECT ID_Topic, Latitude_Topic, Longitude_Topic, CheckTime_Topic FROM Topics;");

		public async Task UpdateTopicCheckTimeAsync(int topicID) => await _dataProvider.ExecuteUpdateAsync("Topics", $"CheckTime_Topic = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", $"where ID_Topic = {topicID}");

		public async Task<object?> LastedDataTimeAsync(int topicID) => await _dataProvider.ExecuteAnySqlScalarAsync($"SELECT MAX(Time_Data) FROM Data WHERE ID_Topic = {topicID};");

		public async Task DeleteTopicAreaAsync(int topicID) => await _dataProvider.ExecuteDeleteAsync("AreaPoints", $"where ID_Topic = {topicID}");

		public async Task InsertTopicAreaAsync(int topicID, PointsPack pointsPack)
		{
			string depressionPoints = $"[{string.Join(", ", pointsPack.DepressionPoints)}]";
			string perimeterPoints = $"[{string.Join(", ", pointsPack.PerimeterPoints)}]";
			string includedPoints = $"[{string.Join(", ", pointsPack.IncludedPoints)}]";
			string islands = string.Empty;

			string values = $"ID_Topic={topicID};Depression_AreaPoint='{depressionPoints}';Perimeter_AreaPoint='{perimeterPoints}';Included_AreaPoint='{includedPoints}';Islands_AreaPoint='{islands}'";
			await _dataProvider.ExecuteInsertAsync("AreaPoints", values);
		}

		public async Task UpdateTopicAreaAsync(int topicID, PointsPack pointsPack)
		{
			string depressionPoints = $"[{string.Join(", ", pointsPack.DepressionPoints)}]";
			string perimeterPoints = $"[{string.Join(", ", pointsPack.PerimeterPoints)}]";
			string includedPoints = $"[{string.Join(", ", pointsPack.IncludedPoints)}]";
			string islands = string.Empty;

			object?[,] data = await GetAreaAsync(topicID);
			string depressionPointsBuffer = data[0, 2]?.ToString() ?? string.Empty;
			string perimeterPointsBuffer = data[0, 3]?.ToString() ?? string.Empty;
			string includedPointsBuffer = data[0, 4]?.ToString() ?? string.Empty;
			string islandsStr = data[0, 5]?.ToString() ?? string.Empty;

			string depressionPointsNew = string.Empty;
			if (!string.IsNullOrEmpty(depressionPointsBuffer))
			{ depressionPointsNew = depressionPointsBuffer.Substring(1, depressionPointsBuffer.Length - 2) + ", " + depressionPoints; }
			string perimeterPointsNew = string.Empty;
			if (!string.IsNullOrEmpty(perimeterPointsBuffer))
			{ perimeterPointsNew = perimeterPointsBuffer.Substring(1, perimeterPointsBuffer.Length - 2) + ", " + perimeterPoints; }
			string includedPointsNew = string.Empty;
			if (!string.IsNullOrEmpty(includedPointsBuffer))
			{ includedPointsNew = includedPointsBuffer.Substring(1, includedPointsBuffer.Length - 2) + ", " + includedPoints; }

			string islandsNew = string.Empty;
			if (!string.IsNullOrEmpty(islandsStr))
			{
				List<Island>? islandsList = JsonSerializer.Deserialize<List<Island>>(islandsStr);
				if (islandsList != null)
				{
					pointsPack.Islands.AddRange(islandsList);
				}
			}

			string values = $"Depression_AreaPoint='{depressionPoints}';Perimeter_AreaPoint='{perimeterPoints}';Included_AreaPoint='{includedPoints}';Islands_AreaPoint='{islands}'";
			await _dataProvider.ExecuteUpdateAsync("AreaPoints", values, $"where ID_Topic = {topicID}");
		}

		//-------------------------------------------------

		public async Task<object?> GetAltitudeAsync(int topicID) => await _dataProvider.ExecuteAnySqlScalarAsync($"SELECT Altitude_Topic FROM Topics WHERE ID_Topic = {topicID};");

		public async Task<object?[,]> GetTopicDataAsync(int topicID) => await _dataProvider.ExecuteSelectTableAsync($"SELECT Value_Data, Time_Data FROM Data WHERE ID_Topic = {topicID} ORDER BY Time_Data DESC LIMIT {Program.COUNT_DATA};");

		public async Task<int> GetCountRowsAsync(int topicID) => await _dataProvider.GetCountRows("Topics", $"where ID_Topic = {topicID}");

		public async Task<object?[,]> GetAreaAsync(int topicID) => await _dataProvider.ExecuteSelectTableAsync($"select * from AreaPoints where ID_Topic = {topicID};");
	}
}
