using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;

namespace Area_Manager_sharp.Queries
{
	internal interface IQueryRepository
	{
		Task<object?[,]> GetTopicsAsync();

		Task UpdateTopicCheckTimeAsync(int topicId);

		public Task<object?> LastedDataTimeAsync(int topicID);

		public Task DeleteTopicAreaAsync(int topicID);

		public Task InsertTopicAreaAsync(int topicID, PointsPack pointsPack);

		public Task UpdateTopicAreaAsync(int topicID, PointsPack pointsPack);

		//-------------------------------------------------

		public Task<object?> GetAltitudeAsync(int topicID);

		public Task<object?[,]> GetTopicDataAsync(int topicID);

		public Task<int> GetCountRowsAsync(int topicID);

		public Task<object?[,]> GetAreaAsync(int topicID);
	}
}
