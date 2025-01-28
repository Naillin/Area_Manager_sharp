using System.Data;
using Microsoft.Data.Sqlite;
using NLog;

namespace Area_Manager_sharp.DBTools
{
	/// <summary>
	/// Базовый набор инструментов для работы с базой данных SQLite.
	/// </summary>
	abstract public class DBBase : IDBBase
	{
		private static readonly string moduleName = "DBBase";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		protected string _connectionString { get; set; }
		/// <summary>
		/// Инициализирует новый экземпляр класса DBBase.
		/// </summary>
		/// <param name="connectionString">Строка подключения к целевой базе данных.</param>
		public DBBase(string connectionString)
		{
			_connectionString = connectionString;
		}

		/// <summary>
		/// Выполнение SQL-функции COUNT(*) без условий и возвратом количества строк.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <returns></returns>
		public int countRows(string table)
		{
			int count = -1;
			string sql = $"select count(*) from {table};";
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand  command = new SqliteCommand (@sql, sqlConnection))
				{
					object? result = command.ExecuteScalar();
					count = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
				}
				sqlConnection.Close();
			}

			return count;
		}

		/// <summary>
		/// Выполнение SQL-функции COUNT(*) с условием и возвратом количества строк.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="conditions">Условия выполнения запроса (обычно начинается с where или join).</param>
		/// <returns></returns>
		public int countRows(string table, string conditions)
		{
			int count = 0;
			string sql = $"select count(*) from {table} {conditions};";
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand  command = new SqliteCommand (@sql, sqlConnection))
				{
					object? result = command.ExecuteScalar();
					count = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
				}
				sqlConnection.Close();
			}

			return count;
		}

		/// <summary>
		/// Возвращает массив информации о всех стлбцах таблицы.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <returns></returns>
		public ColumnsNames[] columnsNames(string table)
		{
			// Запрос для получения информации о столбцах таблицы
			string sql = $"SELECT * FROM {table} LIMIT 1;";
			// Запрос для получения информации о внешних ключах
			string sqlForeignKeys = $"PRAGMA foreign_key_list({table});";
			// Получение информации о внешних ключах
			DataTable dataForeignKeys = new DataTable();
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand commandForeignKeys = new SqliteCommand(@sqlForeignKeys, sqlConnection))
				{
					using (SqliteDataReader readerForeignKeys = commandForeignKeys.ExecuteReader())
					{
						dataForeignKeys.Load(readerForeignKeys);
					}
				}
				sqlConnection.Close();
			}

			// Получение информации о столбцах таблицы
			DataTable data = new DataTable();
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					using (SqliteDataReader reader = command.ExecuteReader())
					{
						data.Load(reader);
					}
				}
				sqlConnection.Close();
			}

			// Создание массива ColumnsNames
			ColumnsNames[] result = new ColumnsNames[data.Columns.Count];
			for (int i = 0; i < data.Columns.Count; i++)
			{
				result[i] = new ColumnsNames
				{
					Name = data.Columns[i].ColumnName,
					LongName = $"{table}.{data.Columns[i].ColumnName}",
					Key = ColumnsNames.BDKeys.NONE // По умолчанию поле не является ключом
				};

				// Проверка, является ли столбец первичным ключом
				if (i == 0) // Предположим, что первый столбец — это первичный ключ
				{
					result[i].Key = ColumnsNames.BDKeys.PK;
				}

				// Проверка, является ли столбец внешним ключом
				foreach (DataRow row in dataForeignKeys.Rows)
				{
					if (data.Columns[i].ColumnName == row["from"].ToString())
					{
						result[i].Key = ColumnsNames.BDKeys.FK;
						result[i].FkParent = row["table"].ToString(); // Таблица, на которую ссылается внешний ключ
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Возвращает массив имен всех таблиц базы данных.
		/// </summary>
		/// <returns></returns>
		public string[] tableNames()
		{
			string sql = "SELECT name FROM sqlite_master WHERE type = 'table';";

			DataTable data = new DataTable();
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					using (SqliteDataReader reader = command.ExecuteReader())
					{
						data.Load(reader);
					}
				}
				sqlConnection.Close();
			}

			string[] result = new string[data.Rows.Count];
			for (int i = 0; i < data.Rows.Count; i++)
			{
				object tableName = data.Rows[i]["name"];
				result[i] = tableName?.ToString() ?? string.Empty;
			}

			return result;
		}

		/// <summary>
		/// Устанавливает режим обработки журнала.
		/// </summary>
		/// <param name="mode">Режим обработки.</param>
		/// <returns></returns>
		public int journalMode(string mode = "WAL")
		{
			int result = -1;
			string sql = $"PRAGMA journal_mode{mode};";
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					result = command.ExecuteNonQuery();
				}
				sqlConnection.Close();
			}

			return result;
		}
	}
}
