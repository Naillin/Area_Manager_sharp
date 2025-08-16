using Area_Manager_sharp.DBTools.DataProvider;
using DBToolsAlias = Area_Manager_sharp.DBTools.DBTools;

namespace Area_Manager_sharp.Queries
{
	internal class DbToolsDataProvider : IDataProvider
	{
		private readonly DBToolsAlias _dBTools;

		public DbToolsDataProvider(DBToolsAlias dBTools)
		{
			_dBTools = dBTools;
			_dBTools.journalMode("WAL");
		}

		public Task<object?[,]> ExecuteSelectTableAsync(string sql, CancellationToken cancellationToken = default) => Task.FromResult(_dBTools.executeSelectTable(sql));

		public Task<object?> ExecuteAnySqlScalarAsync(string sql, CancellationToken cancellationToken = default) => Task.FromResult(_dBTools.executeAnySqlScalar(sql));

		public Task ExecuteUpdateAsync(string table, string value, string conditions, CancellationToken cancellationToken = default) => Task.FromResult(_dBTools.executeUpdate(table, value, conditions));

		public Task ExecuteDeleteAsync(string table, string conditions, CancellationToken cancellationToken = default) => Task.FromResult(_dBTools.executeDelete(table, conditions));

		public Task ExecuteInsertAsync(string table, string value, CancellationToken cancellationToken = default) => Task.FromResult(_dBTools.executeInsert(table, value));

		public Task<int> GetCountRows(string table, string conditions, CancellationToken cancellationToken = default) => Task.FromResult(_dBTools.countRows(table, conditions));
	}
}
