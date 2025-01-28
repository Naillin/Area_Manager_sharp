using System.Data;
using Microsoft.Data.Sqlite;
using NLog;

namespace Area_Manager_sharp.DBTools
{
	/// <summary>
	/// Набор инструментов для изменения данных в базе данных SQLite.
	/// </summary>
	public class DBTools : DBBase
	{
		private static readonly string moduleName = "DBTools";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		/// <summary>
		/// Инициализирует новый экземпляр класса DBTools.
		/// </summary>
		/// <param name="connectionString">Строка подключения к целевой базе данных.</param>
		public DBTools(string connectionString) : base(connectionString) //передача в старший класс
		{
			_connectionString = connectionString;
		}

		/// <summary>
		/// Выполняет любой запрос и возвращает количество затронутых строк.
		/// </summary>
		/// <param name="sql">Запрос соотвествующий всем правилам синтаксиса SQL.</param>
		/// <returns></returns>
		public int executeAnySql(string sql)
		{
			int result = -1;
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

		/// <summary>
		/// Выполняет запрос и возвращает значение первого столбца первой строки результирующего набора, возвращаемого запросом. Дополнительные столбцы или строки не обрабатываются.
		/// </summary>
		/// <param name="sql">Запрос соотвествующий всем правилам синтаксиса SQL.</param>
		/// <returns></returns>
		public object? executeAnySqlScalar(string sql)
		{
			object? result = null;
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					object? resultBuffer = command.ExecuteScalar();
					result = (resultBuffer == DBNull.Value) ? null : resultBuffer;
				}
				sqlConnection.Close();
			}

			return result;
		}

		/// <summary>
		/// Выполнение запроса SELECT или EXECUTE с возвращением двумерного массива данных.
		/// </summary>
		/// <param name="sql">Запрос SELECT соотвествующий всем правилам синтаксиса SQL.</param>
		/// <returns></returns>
		public object?[,] executeSelectTable(string sql)
		{
			object?[,] result = new object?[0, 0];

			if (sql.TrimStart().ToUpper().StartsWith("SELECT") || sql.TrimStart().ToUpper().StartsWith("EXECUTE"))
			{
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

				result = new object[data.Rows.Count, data.Columns.Count];

				for (int i = 0; i < data.Rows.Count; i++)
				{
					for (int j = 0; j < data.Columns.Count; j++)
					{
						result[i, j] = (data.Rows[i][j] == DBNull.Value) ? null : data.Rows[i][j];
					}
				}
				data.Clear();
			}
			else
			{
				//искуственное исключение
				logger.Error("Внимание! Ошибка составления запроса! Учитывайте что запрос должен начинаться с оператора SELECT или EXECUTE!");
			}

			return result;
		}

		/// <summary>
		/// Выполнение запроса SELECT с возвращением таблицы хранимой в DataTable.
		/// </summary>
		/// <param name="sql">Запрос SELECT соотвествующий всем правилам синтаксиса SQL.</param>
		/// <returns></returns>
		public DataTable executeSelectTableDT(string sql)
		{
			DataTable result = new DataTable();
			if (sql.TrimStart().ToUpper().StartsWith("SELECT") || sql.TrimStart().ToUpper().StartsWith("EXECUTE"))
			{
				result.Clear();
				using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
				{
					sqlConnection.Open();
					using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
					{
						using (SqliteDataReader reader = command.ExecuteReader())
						{
							result.Load(reader);
						}
					}
					sqlConnection.Close();
				}
			}
			else
			{
				//искуственное исключение
				logger.Error("Внимание! Ошибка составления запроса! Учитывайте что запрос должен начинаться с оператора SELECT или EXECUTE!");
			}

			return result;
		}

		/// <summary>
		/// Возвращение базы данных хранимой в DataSet. Таблицы данных DataSet имеют связь с таблицой из базы данных с пмощью адаптера. //требуется тестирование
		/// </summary>
		/// <returns></returns>
		public DataSet TakeDatabase()
		{
			DataSet result = new DataSet();

			// Получаем имена всех таблиц в базе данных
			string[] tableNamesMassive = tableNames();

			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();

				// Для каждой таблицы создаем DataTable и заполняем её данными
				foreach (string tableName in tableNamesMassive)
				{
					string sql = $"SELECT * FROM {tableName}";

					// Создаем DataTable
					DataTable dataTable = new DataTable(tableName);

					// Используем SqliteCommand и SqliteDataReader для заполнения DataTable
					using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
					{
						using (SqliteDataReader reader = command.ExecuteReader())
						{
							// Загружаем данные в DataTable
							dataTable.Load(reader);
						}
					}

					// Добавляем DataTable в DataSet
					result.Tables.Add(dataTable);
				}

				sqlConnection.Close();
			}

			return result;
		}

		/// <summary>
		/// Выполнение запроса INSERT для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="value">Принимает массив значений.</param>
		public void executeInsert(string table, string[] value)
		{
			ColumnsNames[] columnsNamesMassive = columnsNames(table); // имена столбцов
			string columns = string.Empty;
			for (int i = 1; i < columnsNamesMassive.Length; i++)
			{
				columns = columns + columnsNamesMassive[i].Name + ", ";
			}
			columns = columns.Remove(columns.Length - 2);

			// Экранируем каждое значение в массиве
			string[] escapedValues = value.Select(v => "'" + v.Replace("'", "''") + "'").ToArray();
			string strValues = string.Join(", ", escapedValues);
			string sql = $"insert into {table} ({columns}) values ({strValues});";

			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					command.ExecuteNonQuery();
				}
				sqlConnection.Close();
			}
		}

		/// <summary>
		/// Выполнение запроса INSERT для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="value">Принимает стороку значений разделенных знаком ';'. Пример: "column1=value1;column2=value2;column3=value3;".</param>
		public void executeInsert(string table, string value)
		{
			if (value.Substring(value.Length - 1) == ";")
			{
				value = value.Remove(value.Length - 1);
			}
			string[] valueMass = value.Split(';');

			//ColumnsNames[] columnsNamesMassive = columnsNames(table); // имена столбцов
			string columns = string.Empty;
			string values = string.Empty;
			for (int i = 0; i < valueMass.Length; i++)
			{
				columns = columns + valueMass[i].Split('=')[0] + ", ";
				values = values + valueMass[i].Split('=')[1] + ", ";
			}
			columns = columns.Remove(columns.Length - 2);
			values = values.Remove(columns.Length - 2);

			string sql = $"insert into {table} ({columns}) values ({values});";

			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					command.ExecuteNonQuery();
				}
				sqlConnection.Close();
			}
		}

		/// <summary>
		/// Выполнение запроса UPDATE для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="value">Принимает массив значений для установки.  (Установит новые значения начиная с первого столбца.)</param>
		/// <param name="conditions">Условия выполнения запроса (обычно начинается с where или join).</param>
		/// <returns></returns>
		public int executeUpdate(string table, string[] value, string conditions)
		{
			int result = -1;
			string strValues = string.Empty;
			ColumnsNames[] columnsNamesMassive = columnsNames(table); // имена столбцов
			for (int i = 1; i <= value.Length; i++) // i = 1, так как 0 столбек это pk
			{
				strValues = strValues + columnsNamesMassive[i].Name + " = " + value[i - 1] + ", ";
			}
			strValues = strValues.Remove(strValues.Length - 2);
			string sql = $"update {table} set {strValues} {conditions};";

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

		/// <summary>
		/// Выполнение запроса UPDATE для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="value">Принимает массив значений для установки. (Установит новые значения начиная с первого столбца.)</param>
		/// <returns></returns>
		public int executeUpdate(string table, string[] value)
		{
			int result = -1;
			string strValues = string.Empty;
			ColumnsNames[] columnsNamesMassive = columnsNames(table); // имена столбцов
			for (int i = 1; i <= value.Length; i++) // i = 1, так как 0 столбек это pk
			{
				strValues = strValues + columnsNamesMassive[i].Name + " = " + value[i - 1] + ", ";
			}
			strValues = strValues.Remove(strValues.Length - 2);
			string sql = $"update {table} set {strValues};";

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

		/// <summary>
		/// Выполнение запроса UPDATE для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="value">Принимает стороку значений разделенных знаком ';'. Пример: "column1=value1;column2=value2;column3=value3;".</param>
		/// <param name="conditions">Условия выполнения запроса (обычно начинается с where или join).</param>
		/// <returns></returns>
		public int executeUpdate(string table, string value, string conditions)
		{
			int result = -1;
			if (value.Substring(value.Length - 1) == ";")
			{
				value = value.Remove(value.Length - 1);
			}
			string strValues = value.Replace(';', ',');

			string sql = $"update {table} set {strValues} {conditions};";
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

		/// <summary>
		/// Выполнение запроса UPDATE для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="value">Принимает стороку значений разделенных знаком ';'. Пример: "column1=value1;column2=value2;column3=value3;".</param>
		/// <returns></returns>
		public int executeUpdate(string table, string value)
		{
			int result = -1;
			if (value.Substring(value.Length - 1) == ";")
			{
				value = value.Remove(value.Length - 1);
			}
			string strValues = value.Replace(';', ',');

			string sql = $"update {table} set {strValues};";
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

		/// <summary>
		/// Выполнение запроса DELETE для заданной таблицы с указанными данными.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="conditions">Условия выполнения запроса (обычно начинается с where или join).</param>
		/// <returns></returns>
		public int executeDelete(string table, string conditions)
		{
			int result = -1;
			string sql = $"delete from {table} {conditions};";
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

		/// <summary>
		/// Выполнение поиска в таблице по значению столбца с возвратом первой строки результата запроса.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="column">Целевой столбец.</param>
		/// <param name="value">Значение поиска.</param>
		/// <returns></returns>
		public object[] searchRecord(string table, string column, string value)
		{
			object[] result = new object[0];

			// Экранируем значение, чтобы избежать SQL-инъекций
			string sql = $"SELECT * FROM {table} WHERE {column} = @value LIMIT 1;";

			DataTable data = new DataTable();
			using (SqliteConnection sqlConnection = new SqliteConnection(_connectionString))
			{
				sqlConnection.Open();
				using (SqliteCommand command = new SqliteCommand(@sql, sqlConnection))
				{
					// Используем параметры для безопасной подстановки значения
					command.Parameters.AddWithValue("@value", value);

					using (SqliteDataReader reader = command.ExecuteReader())
					{
						data.Load(reader);
					}
				}
				sqlConnection.Close();
			}

			// Если найдена хотя бы одна запись
			if (data.Rows.Count > 0)
			{
				result = new object[data.Columns.Count];
				for (int i = 0; i < data.Columns.Count; i++)
				{
					result[i] = data.Rows[0][i];
				}
			}

			return result;
		}
	}
}
