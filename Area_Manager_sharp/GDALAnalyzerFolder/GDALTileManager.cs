using Newtonsoft.Json;
using System.Collections.Generic;

namespace Area_Manager_sharp.GDALAnalyzerFolder
{
	public class GDALTileManager : IDisposable
	{
		private readonly List<TileInfo> _tiles = new List<TileInfo>(); // Список тайлов
		private readonly LRUCache<string, GDALInterface> _cache; // Кэш для GDALInterface
		private readonly string _tilesFolder; // Папка с тайлами
		private readonly string _summaryFile; // Файл с метаданными

		public GDALTileManager(string tilesFolder = "tilesFolder/", string summaryFile = "tilesFolder/summaryFile.json", int cacheSize = 5)
		{
			_tilesFolder = tilesFolder;
			_summaryFile = summaryFile;
			_cache = new LRUCache<string, GDALInterface>(cacheSize); // Инициализация кэша

			if (File.Exists(_summaryFile))
				LoadSummary(); // Загрузка метаданных, если файл существует
			else
				CreateSummary(); // Создание метаданных, если файла нет
		}

		// Создание файла summary.json
		private void CreateSummary()
		{
			foreach (string file in Directory.GetFiles(_tilesFolder, "*.tif"))
			{
				using var gdal = new GDALInterface(file);

				double[] geoTransform = gdal.GetGeoTransform(); // Получаем гео-трансформацию

				double ulx = geoTransform[0]; // Верхний левый угол (X)
				double uly = geoTransform[3]; // Верхний левый угол (Y)
				double xres = geoTransform[1]; // Разрешение по X
				double yres = geoTransform[5]; // Разрешение по Y

				double lrx = ulx + gdal.GetDataset().RasterXSize * xres; // Правый нижний угол (X)
				double lry = uly + gdal.GetDataset().RasterYSize * yres; // Правый нижний угол (Y)

				_tiles.Add(new TileInfo
				{
					Path = file,
					MinLat = Math.Min(lry, uly),
					MaxLat = Math.Max(lry, uly),
					MinLon = Math.Min(ulx, lrx),
					MaxLon = Math.Max(ulx, lrx)
				});
			}

			File.WriteAllText(_summaryFile, JsonConvert.SerializeObject(_tiles)); // Сохраняем метаданные
		}

		// Загрузка метаданных из summary.json
		private void LoadSummary()
		{
			string jsonFile = File.ReadAllText(_summaryFile);
			List<TileInfo>? tileInfos = JsonConvert.DeserializeObject<List<TileInfo>>(jsonFile);
			if (tileInfos != null)
			{
				_tiles.AddRange(tileInfos);
			}
		}

		// Получение высоты по координатам
		public double GetElevation(double lat, double lon)
		{
			foreach (var tile in _tiles.Where(t =>
				lat >= t.MinLat && lat <= t.MaxLat &&
				lon >= t.MinLon && lon <= t.MaxLon))
			{
				var gdal = _cache.Get(tile.Path, p => new GDALInterface(p)); // Используем кэш
				double elevation = gdal.GetElevation(lat, lon);
				if (Math.Abs(elevation - GDALInterface.SeaLevel) > 0.001)
					return elevation;
			}
			return GDALInterface.SeaLevel;
		}

		// Освобождение ресурсов
		public void Dispose()
		{
			_cache.Dispose();
		}
	}

	// Класс для хранения информации о тайле
	public class TileInfo
	{
		public string Path { get; set; } = string.Empty; // Путь к файлу
		public double MinLat { get; set; } // Минимальная широта
		public double MaxLat { get; set; } // Максимальная широта
		public double MinLon { get; set; } // Минимальная долгота
		public double MaxLon { get; set; } // Максимальная долгота
	}
}
