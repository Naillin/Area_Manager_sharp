using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.GDALAnalyzerFolder;
using NLog;
using OSGeo.GDAL;
using System.Text.Json;

namespace Area_Manager_sharp.ElevationAnalyzer
{
	// Класс для всего JSON-ответа
	public class ElevationResponse
	{
		public Result[]? Results { get; set; }
	}

	// Класс для элемента массива results
	public class Result
	{
		public double Latitude { get; set; }
		public double Longitude { get; set; }
		public double Elevation { get; set; }
	}

	internal class ElevationAnalyzer
	{
		private static readonly string moduleName = "area-manager-sharp.ElevationAnalyzer";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private int _delay = 1000;
		private int _maxAttempts = 3;
		private GDALTileManager _gDAL;
		private readonly HttpClient _httpClient;
		bool _debug = false;

		public ElevationAnalyzer(int delay = 1000, int maxAttempts = 3, bool debug = false)
		{
			_delay = delay;
			_maxAttempts = maxAttempts;
			_gDAL = new GDALTileManager();
			_httpClient = new HttpClient();
			_httpClient.Timeout = TimeSpan.FromSeconds(10);
			_debug = debug;
		}

		int countAPI = 0;

		private async Task<double> GetElevationAsync(Сoordinate coordinate)
		{
			double latitude = coordinate.Latitude;
			double longitude = coordinate.Longitude;
			string url = $"https://api.open-elevation.com/api/v1/lookup?locations={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

			double result = -1;
			int attempt = 0;

			while (attempt < _maxAttempts)
			{
				await Task.Delay(_delay);

				try
				{
					HttpResponseMessage response = await _httpClient.GetAsync(url);
					if (response.IsSuccessStatusCode)
					{
						string responseData = await response.Content.ReadAsStringAsync();
						JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
						ElevationResponse? elevationData = JsonSerializer.Deserialize<ElevationResponse>(responseData, options);
						result = elevationData?.Results?[0].Elevation ?? -1;
						countAPI++;

						logger.Info($"Высота точки {latitude}, {longitude}: {result}.");
						break; // Успешный ответ, выходим из цикла
					}
					else
					{
						logger.Warn($"Request failed: {response.StatusCode}. Точка {latitude}, {longitude}. Retrying in 5 seconds...");
						attempt++;
						await Task.Delay(5000);
					}
				}
				catch (Exception ex)
				{
					logger.Error($"Exception! Точка {latitude}, {longitude}. Details: {ex.Message}\n{ex.StackTrace}");
					attempt++;
					await Task.Delay(5000);
				}
			}

			return result;
		}

		private List<Сoordinate> getNeighbors(Сoordinate coordinate, double distance = 200)
		{
			double latitude = coordinate.Latitude;
			double longitude = coordinate.Longitude;

			List<Сoordinate> result = new List<Сoordinate>();

			for (int dLat = -1; dLat < 2; dLat++)
			{
				for (int dLon = -1; dLon < 2; dLon++)
				{
					if (dLat == 0 && dLon == 0) { continue; }

					double newLat = latitude + dLat * (distance / 111320);
					double latInRadians = latitude * Math.PI / 180;
					double newLon = longitude + dLon * (distance / (111320 * Math.Cos(latInRadians)));

					result.Add(new Сoordinate(newLat, newLon));
				}
			}

			return result;
		}

		private bool areNeighbors(Сoordinate coordinate1, Сoordinate coordinate2, double checkDistance = 200)
		{
			double latitude1 = coordinate1.Latitude;
			double longitude1 = coordinate1.Longitude;
			double latitude2 = coordinate2.Latitude;
			double longitude2 = coordinate2.Longitude;

			double distance = Math.Sqrt(Math.Pow(latitude1 - latitude2, 2) + Math.Pow(longitude1 - longitude2, 2));
			return (distance <= (checkDistance / 111320));
		}

		private struct CheckPoint
		{
			public Сoordinate _сoordinate;
			public double _height;

			public CheckPoint (Сoordinate сoordinate, double height)
			{
				this._сoordinate = сoordinate;
				this._height = height;
			}
		}

		// делегат для оператора сравнения
		public delegate bool ComparisonOperator(double a, double b);
		public async Task<PointsPack> findArea(Сoordinate coordinate, double initialHeight = 200, double distance = 200, bool equalOption = false, bool useInflux = false)
		{
			List<Сoordinate> depressionPoints = new List<Сoordinate>();
			List<Сoordinate> perimeterPoints = new List<Сoordinate>();
			List<Сoordinate> includedPoints = new List<Сoordinate>();
			List<Сoordinate> nonFloodedPoints = new List<Сoordinate>();
			List<Island> islands = new List<Island>();
			int islandID = 0;

			Queue<CheckPoint> pointsToCheck = new Queue<CheckPoint>();
			List<Сoordinate> checkedPoints = new List<Сoordinate>();
			pointsToCheck.Enqueue(new CheckPoint(coordinate, initialHeight));

			Program program = new Program();
			int countDebug = 0;

			ComparisonOperator comparison;
			if (equalOption)
			{
				comparison = (a, b) => a <= b; // Используем оператор <=
			}
			else
			{
				comparison = (a, b) => a < b; // Используем оператор <
			}

			while (pointsToCheck.Count != 0)
			{
				CheckPoint checkPoint = pointsToCheck.Dequeue(); // Берем первый элемент и удаляем его

				if (checkedPoints.Contains(checkPoint._сoordinate))
				{
					continue;
				}
				checkedPoints.Add(checkPoint._сoordinate);

				double currentElevation = await GetElevationAsync(checkPoint._сoordinate);
				logger.Info($"Высота проверяемой точки: {checkPoint._height}.");
				double modedElevation = currentElevation;
				if (useInflux)
				{
					modedElevation = Math.Sqrt(currentElevation * checkPoint._height);
					logger.Info($"Модифицированная высота проверяемой точки: {modedElevation}.");
				}
				if (comparison(currentElevation, checkPoint._height))
				{
					depressionPoints.Add(checkPoint._сoordinate);
					List<Сoordinate> neighbors = getNeighbors(checkPoint._сoordinate, distance);
					foreach (Сoordinate neighbor in neighbors)
					{
						if (!checkedPoints.Contains(neighbor))
						{
							pointsToCheck.Enqueue(new CheckPoint(neighbor, modedElevation)); // Добавляем в очередь
						}
					}
					countDebug++;
				}
				else
				{
					nonFloodedPoints.Add(checkPoint._сoordinate);
				}
				logger.Info($"Количество точек для проверки: {pointsToCheck.Count}.");

				if (_debug && countDebug >= 10)
				{
					PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
					program.insertAreaData(resultDebug, Program.TopicID, false, true);
					logger.Info($"Number of requests to API: {countAPI}.");
				}
			}

			foreach (Сoordinate point in depressionPoints)
			{
				List<Сoordinate> neighbors = getNeighbors(point, distance);

				bool hasNonFloodedNeighbor = false;
				foreach (Сoordinate neighbor in neighbors)
				{
					if (!depressionPoints.Contains(neighbor))
					{
						hasNonFloodedNeighbor = true;
						if (nonFloodedPoints.Contains(neighbor))
						{
							perimeterPoints.Add(neighbor);
						}
						else
						{
							includedPoints.Add(point);
						}
					}
				}

				if (!hasNonFloodedNeighbor)
				{
					// Ищем остров с соседними координатами
					Island? existingIsland = islands
						.FirstOrDefault(island => island.Coords
						.Any(coordinate => areNeighbors(coordinate, point, 200)));

					if (existingIsland != null)
					{
						// Если остров найден, добавляем точку в его координаты
						existingIsland.Coords.Add(point);
					}
					else
					{
						// Если остров не найден, создаем новый
						existingIsland = new Island(islandID, new List<Сoordinate> { point });
						islands.Add(existingIsland);
						islandID++;
					}
				}
			}

			PointsPack result = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
			logger.Info($"Depression Points: {string.Join("; ", depressionPoints)}.");
			logger.Info($"Perimeter Points: {string.Join("; ", perimeterPoints)}.");
			logger.Info($"Included Points: {string.Join(";", includedPoints)}.");
			logger.Info($"Islands: {string.Join("; ", islands)}.");
			logger.Info($"Number of requests to API: {countAPI}.");
			return result;
		}

		private double GetElevationGDAL(Сoordinate coordinate)
		{
			double latitude = coordinate.Latitude;
			double longitude = coordinate.Longitude;

			double result = -1;

			try
			{
				result = _gDAL.GetElevation(latitude, longitude);
				logger.Info($"Высота точки {latitude}, {longitude}: {result}.");
			}
			catch (Exception ex)
			{
				logger.Error($"Exception! Точка {latitude}, {longitude}. Details: {ex.Message}\n{ex.StackTrace}");
			}

			return result;
		}

		public PointsPack findAreaGDAL(Сoordinate coordinate, double initialHeight = 200, double distance = 200, bool equalOption = false, bool useInflux = false)
		{
			List<Сoordinate> depressionPoints = new List<Сoordinate>();
			List<Сoordinate> perimeterPoints = new List<Сoordinate>();
			List<Сoordinate> includedPoints = new List<Сoordinate>();
			List<Сoordinate> nonFloodedPoints = new List<Сoordinate>();
			List<Island> islands = new List<Island>();
			int islandID = 0;

			Queue<CheckPoint> pointsToCheck = new Queue<CheckPoint>();
			List<Сoordinate> checkedPoints = new List<Сoordinate>();
			pointsToCheck.Enqueue(new CheckPoint(coordinate, initialHeight));

			Program program = new Program();
			int countDebug = 0;

			ComparisonOperator comparison;
			if (equalOption)
			{
				comparison = (a, b) => a <= b; // Используем оператор <=
			}
			else
			{
				comparison = (a, b) => a < b; // Используем оператор <
			}

			while (pointsToCheck.Count != 0)
			{
				CheckPoint checkPoint = pointsToCheck.Dequeue(); // Берем первый элемент и удаляем его

				if (checkedPoints.Contains(checkPoint._сoordinate))
				{
					continue;
				}
				checkedPoints.Add(checkPoint._сoordinate);

				double currentElevation = GetElevationGDAL(checkPoint._сoordinate);
				logger.Info($"Высота проверяемой точки: {checkPoint._height}.");
				double modedElevation = currentElevation;
				if (useInflux)
				{
					modedElevation = Math.Sqrt(currentElevation * checkPoint._height);
					logger.Info($"Модифицированная высота проверяемой точки: {modedElevation}.");
				}
				if (comparison(currentElevation, checkPoint._height))
				{
					depressionPoints.Add(checkPoint._сoordinate);
					List<Сoordinate> neighbors = getNeighbors(checkPoint._сoordinate, distance);
					foreach (Сoordinate neighbor in neighbors)
					{
						if (!checkedPoints.Contains(neighbor))
						{
							pointsToCheck.Enqueue(new CheckPoint(neighbor, modedElevation)); // Добавляем в очередь
						}
					}
					countDebug++;
				}
				else
				{
					nonFloodedPoints.Add(checkPoint._сoordinate);
				}
				logger.Info($"Количество точек для проверки: {pointsToCheck.Count}.");

				if (_debug && countDebug >= 10)
				{
					PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
					program.insertAreaData(resultDebug, Program.TopicID, false, true);
				}
			}

			foreach (Сoordinate point in depressionPoints)
			{
				List<Сoordinate> neighbors = getNeighbors(point, distance);

				bool hasNonFloodedNeighbor = false;
				foreach (Сoordinate neighbor in neighbors)
				{
					if (!depressionPoints.Contains(neighbor))
					{
						hasNonFloodedNeighbor = true;
						if (nonFloodedPoints.Contains(neighbor))
						{
							perimeterPoints.Add(neighbor);
						}
						else
						{
							includedPoints.Add(point);
						}
					}
				}

				if (!hasNonFloodedNeighbor)
				{
					// Ищем остров с соседними координатами
					Island? existingIsland = islands
						.FirstOrDefault(island => island.Coords
						.Any(coordinate => areNeighbors(coordinate, point, 200)));

					if (existingIsland != null)
					{
						// Если остров найден, добавляем точку в его координаты
						existingIsland.Coords.Add(point);
					}
					else
					{
						// Если остров не найден, создаем новый
						existingIsland = new Island(islandID, new List<Сoordinate> { point });
						islands.Add(existingIsland);
						islandID++;
					}
				}
			}

			PointsPack result = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
			logger.Info($"Depression Points: {string.Join("; ", depressionPoints)}.");
			logger.Info($"Perimeter Points: {string.Join("; ", perimeterPoints)}.");
			logger.Info($"Included Points: {string.Join(";", includedPoints)}.");
			logger.Info($"Islands: {string.Join("; ", islands)}.");
			return result;
		}
	}
}
