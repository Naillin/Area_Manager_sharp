using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;

namespace Area_Manager_sharp.ElevationAnalyzerFolder
{
	internal abstract class ElevationAnalyzer : IElevationAnalyzer
	{
		private LoggerManager logger;
		// делегат для оператора сравнения
		protected delegate bool ComparisonOperator(double a, double b);
		protected ComparisonOperator comparison;

		public ElevationAnalyzer(LoggerManager logger)
		{
			this.logger = logger;

			if (Program.EQUAL_OPTION)
			{
				comparison = (a, b) => a <= b; // Используем оператор <=
			}
			else
			{
				comparison = (a, b) => a < b; // Используем оператор <
			}
		}

		protected List<Coordinate> GetNeighbors(Coordinate coordinate, double distance = 200)
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

		protected bool AreNeighbors(Coordinate coordinate1, Coordinate coordinate2, double checkDistance = 200)
		{
			double latitude1 = coordinate1.Latitude;
			double longitude1 = coordinate1.Longitude;
			double latitude2 = coordinate2.Latitude;
			double longitude2 = coordinate2.Longitude;

			double distance = Math.Sqrt(Math.Pow(latitude1 - latitude2, 2) + Math.Pow(longitude1 - longitude2, 2));
			return (distance <= (checkDistance / 111320));
		}

		protected struct CheckPoint
		{
			public Coordinate _сoordinate;
			public double _height;

			public CheckPoint (Coordinate сoordinate, double height)
			{
				this._сoordinate = сoordinate;
				this._height = height;
			}
		}

		protected List<Coordinate> GenerateCirclePoints(Coordinate center, double stepDistanceMeters = 30, double circleRadiusMeters = 10000)
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

		public abstract PointsPack FindArea(Coordinate coordinate, double initialHeight);
	}
}
