
namespace Area_Manager_sharp.DBTools
{
	/// <summary>
	/// Структура хранения информации о полях таблицы.
	/// </summary>
	public struct ColumnsNames
	{
		/// <summary>
		/// Типы ключей базы данных.
		/// </summary>
		public enum BDKeys
		{
			NONE = 0, PK = 1, FK = 2
		}

		public string? Name;
		public string? LongName;
		public BDKeys Key;
		public string? FkParent;

		/// <summary>
		/// Структура хранения информации о полях таблицы.
		/// </summary>
		/// <param name="name">Имя таблицы.</param>
		/// <param name="longName">Полное имя таблицы.</param>
		/// <param name="key">Тип ключа.</param>
		/// <param name="fkParent">Родительская таблица.</param>
		public ColumnsNames(string? name = null, string? longName = null, BDKeys key = BDKeys.NONE, string? fkParent = null)
		{
			this.Name = name;
			this.LongName = longName;
			this.Key = key;
			this.FkParent = fkParent;
		}
	}
}
