
namespace Area_Manager_sharp.GDALAnalyzerFolder
{
	public class LRUCache<TKey, TValue> : IDisposable where TKey : notnull where TValue : IDisposable
	{
		private readonly int _capacity; // Максимальный размер кэша
		private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _cache; // Словарь для быстрого доступа
		private readonly LinkedList<(TKey Key, TValue Value)> _list; // Список для отслеживания порядка использования

		public LRUCache(int capacity)
		{
			_capacity = capacity;
			_cache = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity);
			_list = new LinkedList<(TKey Key, TValue Value)>();
		}

		// Получение значения из кэша
		public TValue Get(TKey key, Func<TKey, TValue> factory)
		{
			if (_cache.TryGetValue(key, out var node))
			{
				// Перемещаем элемент в начало списка (последний использованный)
				_list.Remove(node);
				_list.AddFirst(node);
				return node.Value.Value;
			}

			// Если элемента нет в кэше, создаем его
			var value = factory(key);
			var newNode = new LinkedListNode<(TKey Key, TValue Value)>((key, value));
			_cache.Add(key, newNode);
			_list.AddFirst(newNode);

			// Если кэш переполнен, удаляем последний элемент
			if (_cache.Count > _capacity)
			{
				var last = _list.Last;
				if (last != null && last.Value.Key != null)
				{
					_cache.Remove(last.Value.Key);
					last.Value.Value.Dispose(); // Освобождаем ресурсы
				}
				_list.RemoveLast();
			}

			return value;
		}

		// Освобождение ресурсов
		public void Dispose()
		{
			foreach (var item in _cache.Values)
				item.Value.Value.Dispose(); // Освобождаем все ресурсы
			_cache.Clear();
			_list.Clear();
		}
	}
}
