
namespace Area_Manager_sharp.DBTools
{
	/// <summary>
	/// Интерфейс базового набора инструментов для работы с базой данных SQLite.
	/// </summary>
	internal interface IDBBase
	{
		/// <summary>
		/// Выполнение SQL-функции COUNT(*) без условий и возвратом количества строк.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <returns></returns>
		int countRows(string table);

		/// <summary>
		/// Выполнение SQL-функции COUNT(*) с условием и возвратом количества строк.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <param name="conditions">Условия выполнения запроса (обычно начинается с where или join).</param>
		/// <returns></returns>
		int countRows(string table, string conditions);

		/// <summary>
		/// Возвращает массив информации о всех стлбцах таблицы.
		/// </summary>
		/// <param name="table">Целевая таблица.</param>
		/// <returns></returns>
		ColumnsNames[] columnsNames(string table);

		/// <summary>
		/// Возвращает массив имен всех таблиц базы данных.
		/// </summary>
		/// <returns></returns>
		string[] tableNames();

		/// <summary>
		/// Устанавливает режим обработки журнала.
		/// </summary>
		/// <param name="mode">Режим обработки.</param>
		/// <returns></returns>
		int journalMode(string mode = "WAL");
	}
}
