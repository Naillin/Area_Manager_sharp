using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.GDALAnalyzerFolder;
using NLog;

namespace Area_Manager_sharp.ElevationAnalyzerFolder
{
	internal class PythonPointAnalyzer : ElevationAnalyzer
	{
		private static readonly string moduleName = "PythonPointAnalyzer";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private bool _useInflux = false;
		private double _distance = 200;
		private GDALPython _gDALPython;
		private bool _debug = false;

		public PythonPointAnalyzer() : base(logger)
		{
			_distance = Program.DISTANCE;
			_gDALPython = new GDALPython(Program.GDAL_PYTHON, Program.GDAL_PYSCRIPT);
			_useInflux = Program.USE_INFLUX;
			_debug = Program.DEBUG_MODE;
		}

		
		public override PointsPack FindArea(Coordinate coordinate, double initialHeight)
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

				double currentElevation = _gDALPython.GetElevation(checkPoint._сoordinate);
				logger.Info($"Высота проверяемой точки: {checkPoint._height}.");
				double modedElevation = currentElevation;
				if (_useInflux)
				{
					modedElevation = Math.Sqrt(currentElevation * checkPoint._height);
					logger.Info($"Модифицированная высота проверяемой точки: {modedElevation}.");
				}
				if (comparison(currentElevation, checkPoint._height))
				{
					depressionPoints.Add(checkPoint._сoordinate);
					List<Coordinate> neighbors = GetNeighbors(checkPoint._сoordinate, _distance);
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

				//if (_debug && countDebug >= 100)
				//{
				//	PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
				//	program.insertAreaData(resultDebug, Program.TopicID, true, true);
				//	countDebug = 0;
				//	Thread.Sleep(1000);
				//}
			}
			_gDALPython.Dispose();
			logger.Info($"Затопленные точки вычеслены.");

			foreach (Coordinate point in depressionPoints)
			{
				List<Coordinate> neighbors = GetNeighbors(point, _distance);

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
						.Any(coordinate => AreNeighbors(coordinate, point, 200)));

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
	}
}
