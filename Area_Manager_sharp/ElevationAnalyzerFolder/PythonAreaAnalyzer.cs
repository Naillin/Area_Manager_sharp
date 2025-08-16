using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.GDALAnalyzerFolder;
using NLog;

namespace Area_Manager_sharp.ElevationAnalyzerFolder
{
	internal class PythonAreaAnalyzer : ElevationAnalyzer
	{
		private static readonly string moduleName = "PythonAreaAnalyzer";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private double _distance = 200;
		private double _radius = 10000;
		private int _countOfSubs = 100;
		private double _coefHeight = 2.0;
		private GDALPython _gDALPython;
		private bool _debug = false;

		public PythonAreaAnalyzer() : base(logger)
		{
			_distance = Program.DISTANCE;
			_radius = Program.RADIUS;
			_countOfSubs = Program.COUNT_OF_SUBS;
			_coefHeight = Program.COEF_HEIGHT;
			_gDALPython = new GDALPython(Program.GDAL_PYTHON, Program.GDAL_PYSCRIPT);
			_debug = Program.DEBUG_MODE;
		}

		public override PointsPack FindArea(Coordinate coordinate, double initialHeight = 100)
		{
			List<Coordinate> depressionPoints = new List<Coordinate>();
			List<Coordinate> perimeterPoints = new List<Coordinate>();
			List<Coordinate> includedPoints = new List<Coordinate>();
			List<Coordinate> nonFloodedPoints = new List<Coordinate>();
			List<Island> islands = new List<Island>();

			Program program = new Program();

			HashSet<Coordinate> checkedPoints = new HashSet<Coordinate>();
			double stepForHeight = (initialHeight / _coefHeight) / (double)_countOfSubs;
			double stepForRadius = _radius / (double)_countOfSubs;

			logger.Info($"Вычисление затопленных точек.");
			_gDALPython.StartPythonProcess();
			if (_coefHeight != -1)
			{
				for (double currentRadius = stepForRadius; currentRadius <= 10000; currentRadius = currentRadius + stepForRadius)
				{
					List<Coordinate> circleCoordinates = GenerateCirclePoints(coordinate, _distance, currentRadius);
					foreach (Coordinate item in circleCoordinates)
					{
						if (!checkedPoints.Contains(item))
						{
							checkedPoints.Add(item);

							double currentElevation = _gDALPython.GetElevation(item);
							//logger.Info($"Высота проверяемой точки: {currentElevation}.");
							if (comparison(currentElevation, initialHeight))
							{
								depressionPoints.Add(item);
							}
						}
					}

					//if (_debug)
					//{
					//	PointsPack resultDebug = new PointsPack(depressionPoints, perimeterPoints, includedPoints, islands);
					//	program.insertAreaData(resultDebug, Program.TopicID, true, true);
					//	Thread.Sleep(1000);
					//}

					initialHeight = initialHeight - stepForHeight;
				}
			}
			else
			{
				List<Coordinate> circleCoordinates = GenerateCirclePoints(coordinate, _distance, _radius);
				foreach (Coordinate item in circleCoordinates)
				{
					if (!checkedPoints.Contains(item))
					{
						checkedPoints.Add(item);

						double currentElevation = _gDALPython.GetElevation(item);
						//logger.Info($"Высота проверяемой точки: {currentElevation}.");
						if (comparison(currentElevation, initialHeight))
						{
							depressionPoints.Add(item);
						}
					}
				}
			}
			_gDALPython.Dispose();
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
