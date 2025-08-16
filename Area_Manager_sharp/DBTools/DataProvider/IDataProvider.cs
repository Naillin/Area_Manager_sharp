namespace Area_Manager_sharp.DBTools.DataProvider
{
	internal interface IDataProvider
	{
		Task<object?[,]> ExecuteSelectTableAsync(string sql, CancellationToken cancellationToken = default);

		Task<object?> ExecuteAnySqlScalarAsync(string sql, CancellationToken cancellationToken = default);

		Task ExecuteUpdateAsync(string table, string value, string conditions, CancellationToken cancellationToken = default);

		Task ExecuteDeleteAsync(string table, string conditions, CancellationToken cancellationToken = default);

		Task ExecuteInsertAsync(string table, string values, CancellationToken cancellationToken = default);

		Task<int> GetCountRows(string table, string conditions, CancellationToken cancellationToken = default);
	}
}
