using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.GDALAnalyzerFolder;
using NLog;
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
		private static readonly string moduleName = "ElevationAnalyzer";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private int _delay = 1000;
		private int _maxAttempts = 3;
		private double _distance = 200;
		private bool _equalOption = false;
		//private GDALTileManager _gDAL;
		private GDALPython _gDALPython;
		private readonly HttpClient _httpClient;
		bool _debug = false;

		public ElevationAnalyzer(int delay = 1000, int maxAttempts = 3, double distance = 200, bool equalOption = false, bool debug = false)
		{
			_delay = delay;
			_maxAttempts = maxAttempts;
			_distance = distance;
			_equalOption = equalOption;
			//_gDAL = new GDALTileManager(Program.TILES_FOLDER, $"{Program.TILES_FOLDER}/summaryFile.json");
			_gDALPython = new GDALPython(Program.GDAL_PYTHON, Program.GDAL_PYSCRIPT);
			_httpClient = new HttpClient();
			_httpClient.Timeout = TimeSpan.FromSeconds(10);
			_debug = debug;
		}

		int countAPI = 0;

		private async Task<double> GetElevationAsync(Coordinate coordinate)
		{
			double latitude = coordinate.Latitude;
			double longitude = coordinate.Longitude;
			string url = $"https://api.open-elevation.com/api/v1/lookup?locations={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

			double result = -32768;
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

		private List<Coordinate> getNeighbors(Coordinate coordinate, double distance = 200)
		{
			double latitude = coordinate.Latitude;
			double longitude = coordinate.Longitude;

			List<Coordinate> result = new List<Coordinate>();

			for (int dLat = -1; dLat < 2; dLat++)
			{
				for (int dLon = -1; dLon < 2; dLon++)
				{
					if (dLat == 0 && dLon == 0) { continue; }

					double newLat = latitude + dLat * (distance / 111320);
					double latInRadians = latitude * Math.PI / 180;
					double newLon = longitude + dLon * (distance / (111320 * Math.Cos(latInRadians)));

					result.Add(new Coordinate(newLat, newLon));
				}
			}

			return result;
		}

		private bool areNeighbors(Coordinate coordinate1, Coordinate coordinate2, double checkDistance = 200)
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
			public Coordinate _сoordinate;
			public double _height;

			public CheckPoint (Coordinate сoordinate, double height)
			{
				this._сoordinate = сoordinate;
				this._height = height;
			}
		}

		// делегат для оператора сравнения
		public delegate bool ComparisonOperator(double a, double b);
		public async Task<PointsPack> findArea(Coordinate coordinate, double initialHeight, bool useInflux = false)
		{
			List<Coordinate> depressionPoints = new List<Coordinate>();
			List<Coordinate> perimeterPoints = new List<Coordinate>();
			List<Coordinate> includedPoints = new List<Coordinate>();
			List<Coordinate> nonFloodedPoints = new List<Coordinate>();
			List<Island> islands = new List<Island>();
			int islandID = 0;

			Queue<CheckPoint> pointsToCheck = new Queue<CheckPoint>();
			HashSet<Coordinate> checkedPoints = new HashSet<Coordinate>();
			pointsToCheck.Enqueue(new CheckPoint(coordinate, initialHeight));

			Program program = new Program();
			int countDebug = 0;

			ComparisonOperator comparison;
			if (_equalOption)
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
					List<Coordinate> neighbors = getNeighbors(checkPoint._сoordinate, _distance);
					foreach (Coordinate neighbor in neighbors)
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

				if (_debug && countDebug >= 100)
				{
					PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
					program.insertAreaData(resultDebug, Program.TopicID, true, true);
					countDebug = 0;
					await Task.Delay(1000);
				}
			}

			foreach (Coordinate point in depressionPoints)
			{
				List<Coordinate> neighbors = getNeighbors(point, _distance);

				bool hasNonFloodedNeighbor = false;
				foreach (Coordinate neighbor in neighbors)
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
						existingIsland = new Island(islandID, new List<Coordinate> { point });
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

		private double GetElevationGDAL(Coordinate coordinate)
		{
			double latitude = coordinate.Latitude;
			double longitude = coordinate.Longitude;

			double result = -1;

			try
			{
				//result = _gDAL.GetElevation(latitude, longitude);
				result = _gDALPython.GetElevation(coordinate);
				logger.Info($"Высота точки {latitude}, {longitude}: {result}.");
			}
			catch (Exception ex)
			{
				logger.Error($"Exception! Точка {latitude}, {longitude}. Details: {ex.Message}\n{ex.StackTrace}");
			}

			return result;
		}

		public PointsPack findAreaGDAL(Coordinate coordinate, double initialHeight, bool useInflux = false)
		{
			List<Coordinate> depressionPoints = new List<Coordinate>();
			List<Coordinate> perimeterPoints = new List<Coordinate>();
			List<Coordinate> includedPoints = new List<Coordinate>();
			List<Coordinate> nonFloodedPoints = new List<Coordinate>();
			List<Island> islands = new List<Island>();
			int islandID = 0;

			Queue<CheckPoint> pointsToCheck = new Queue<CheckPoint>();
			HashSet<Coordinate> checkedPoints = new HashSet<Coordinate>();
			pointsToCheck.Enqueue(new CheckPoint(coordinate, initialHeight));

			Program program = new Program();
			int countDebug = 0;

			ComparisonOperator comparison;
			if (_equalOption)
			{
				comparison = (a, b) => a <= b; // Используем оператор <=
			}
			else
			{
				comparison = (a, b) => a < b; // Используем оператор <
			}

			logger.Info($"Вычисление затопленных точек.");
			_gDALPython.StartPythonProcess();
			while (pointsToCheck.Count != 0)
			{
				CheckPoint checkPoint = pointsToCheck.Dequeue(); // Берем первый элемент и удаляем его

				bool continueCoord = false;
				foreach (Coordinate сoord in checkedPoints)
				{
					if (checkPoint._сoordinate.Latitude == сoord.Latitude &&
						checkPoint._сoordinate.Longitude == сoord.Longitude)
					{
						continueCoord = true;
						break;
					}
				}
				if (continueCoord || checkedPoints.Contains(checkPoint._сoordinate))
				{
					continue;
				}
				checkedPoints.Add(checkPoint._сoordinate);

				//double currentElevation = GetElevationGDAL(checkPoint._сoordinate);
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
					List<Coordinate> neighbors = getNeighbors(checkPoint._сoordinate, _distance);
					foreach (Coordinate neighbor in neighbors)
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

				if (_debug && countDebug >= 100)
				{
					PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
					program.insertAreaData(resultDebug, Program.TopicID, true, true);
					countDebug = 0;
					Thread.Sleep(1000);
				}
			}
			_gDALPython.StopPythonProcess();
			logger.Info($"Затопленные точки вычеслены.");

			foreach (Coordinate point in depressionPoints)
			{
				List<Coordinate> neighbors = getNeighbors(point, _distance);

				bool hasNonFloodedNeighbor = false;
				foreach (Coordinate neighbor in neighbors)
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
						existingIsland = new Island(islandID, new List<Coordinate> { point });
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

		public static List<Coordinate> GenerateCirclePoints(Coordinate center, double stepDistanceMeters = 30, double circleRadiusMeters = 10000)
		{
			List<Coordinate> circlePoints = new List<Coordinate>();
			circlePoints.Add(center); // Добавляем центр круга в результат

			// Преобразуем метры в градусы для широты (1 градус широты ≈ 111320 метров)
			double stepDistanceDegreesLat = stepDistanceMeters / 111320.0;
			double circleRadiusDegreesLat = circleRadiusMeters / 111320.0;

			// Учитываем, что длина градуса долготы зависит от широты
			double centerLatRadians = center.Latitude * Math.PI / 180.0; // Широта центра в радианах
			double metersPerDegreeLon = 111320.0 * Math.Cos(centerLatRadians); // Длина градуса долготы на текущей широте
			double stepDistanceDegreesLon = stepDistanceMeters / metersPerDegreeLon; // Шаг для долготы в градусах
			double circleRadiusDegreesLon = circleRadiusMeters / metersPerDegreeLon; // Радиус для долготы в градусах

			logger.Info($"Начало генерации точек круга.");

			// Генерация точек круга
			for (double currentRadiusDegrees = 0; currentRadiusDegrees <= circleRadiusDegreesLat; currentRadiusDegrees += stepDistanceDegreesLat)
			{
				// Количество шагов для текущего радиуса
				int numberOfSteps = (int)(2 * Math.PI * currentRadiusDegrees / stepDistanceDegreesLat);

				for (int stepIndex = 0; stepIndex < numberOfSteps; stepIndex++)
				{
					// Угол для текущей точки
					double angle = stepIndex * (2 * Math.PI / numberOfSteps);

					// Вычисляем координаты точки
					double pointLat = center.Latitude + currentRadiusDegrees * Math.Cos(angle); // Широта точки
					double pointLon = center.Longitude + currentRadiusDegrees * Math.Sin(angle) * (stepDistanceDegreesLon / stepDistanceDegreesLat); // Долгота точки

					// Добавляем точку в результат
					circlePoints.Add(new Coordinate(pointLat, pointLon));
				}
			}

			logger.Info($"Генерация точек круга завершена. Сгенерировано {circlePoints.Count} точек.");

			return circlePoints;
		}

		public PointsPack findAreaFigureGDAL(Coordinate coordinate, double initialHeight = 100, double radius = 10000, int countOfSubs = 100)
		{
			List<Coordinate> depressionPoints = new List<Coordinate>();
			List<Coordinate> perimeterPoints = new List<Coordinate>();
			List<Coordinate> includedPoints = new List<Coordinate>();
			List<Coordinate> nonFloodedPoints = new List<Coordinate>();
			List<Island> islands = new List<Island>();

			Program program = new Program();

			HashSet<Coordinate> checkedPoints = new HashSet<Coordinate>();
			double stepForHeight = (initialHeight / 1.0) / countOfSubs;
			double stepForRadius = radius / countOfSubs;

			ComparisonOperator comparison;
			if (_equalOption)
			{
				comparison = (a, b) => a <= b; // Используем оператор <=
			}
			else
			{
				comparison = (a, b) => a < b; // Используем оператор <
			}

			logger.Info($"Вычисление затопленных точек.");
			_gDALPython.StartPythonProcess();
			for (double currentRadius = stepForRadius; currentRadius <= 10000; currentRadius = currentRadius + stepForRadius)
			{
				List<Coordinate> circleCoordinates = GenerateCirclePoints(coordinate, _distance, currentRadius);
				foreach (Coordinate item in circleCoordinates)
				{
					if (!checkedPoints.Contains(item))
					{
						checkedPoints.Add(item);

						double currentElevation = GetElevationGDAL(item);
						logger.Info($"Высота проверяемой точки: {currentElevation}.");
						if (comparison(currentElevation, initialHeight))
						{
							depressionPoints.Add(item);
						}
					}
				}

				if (_debug)
				{
					PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
					program.insertAreaData(resultDebug, Program.TopicID, true, true);
					Thread.Sleep(1000);
				}

				initialHeight = initialHeight - stepForHeight;
			}
			_gDALPython.StopPythonProcess();
			depressionPoints = depressionPoints.Distinct().ToList();
			logger.Info($"Затопленные точки вычеслены.");

			PointsPack result = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
			logger.Info($"Depression Points: {string.Join("; ", depressionPoints)}.");
			logger.Info($"Perimeter Points: {string.Join("; ", perimeterPoints)}.");
			logger.Info($"Included Points: {string.Join(";", includedPoints)}.");
			logger.Info($"Islands: {string.Join("; ", islands)}.");
			return result;
		}
	}
}
