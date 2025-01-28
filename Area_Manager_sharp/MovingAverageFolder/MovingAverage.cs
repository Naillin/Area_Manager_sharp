using NLog;

namespace Area_Manager_sharp.MovingAverage
{
	internal class MovingAverage
	{
		private static readonly string moduleName = "MovingAverage";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private double _windowSize;

		public MovingAverage(double windowSize = 7)
		{
			_windowSize = windowSize;
		}

		public List<DataUnit> CalculateEmaSmooth(List<DataUnit> data, double smoothing = 2, double slopeFactor = 1)
		{
			if (data == null || data.Count.Equals(0))
			{
				return new List<DataUnit>();
			}

			double alpha = smoothing / (_windowSize + 1);
			double ema = data[0].valueData ?? 0.0;
			List<DataUnit> emaData = new List<DataUnit>();

			for (int i = 0; i < data.Count; i++)
			{
				if (i < _windowSize - 1)
				{
					emaData.Add(new DataUnit(null, data[i].timeData));
				}
				else
				{
					ema = alpha * (double)(data[i].valueData ?? 0.0) + (1 - alpha) * ema;
					emaData.Add(new DataUnit(ema, data[i].timeData));
					logger.Info($"Moving average at index {i}: {ema}");
				}
			}

			List<DataUnit> lastDates = data.TakeLast((int)_windowSize).ToList();
			double lastEma = ema;
			logger.Info($"Last ema: {lastEma}");

			List<long> timeIntervals = new List<long>();
			for (int i = 1; i < lastDates.Count; i++)
			{
				timeIntervals.Add(lastDates[i].timeData - lastDates[i - 1].timeData);
			}
			TimeSpan averageTimeInterval = TimeSpan.FromSeconds(timeIntervals.Average());
			logger.Info($"Average time interval: {averageTimeInterval}");

			double slope = (double)((lastDates.Last().valueData ?? 0.0) - (lastDates.First().valueData ?? 0.0)) / (_windowSize - 1);
			logger.Info($"Slope: {slope}");
			for (int i = 0; i < 3; i++)
			{
				DataUnit dataUnit = new DataUnit
					(
						lastEma + slope * (i + slopeFactor),
						lastDates.Last().timeData + (long)(averageTimeInterval.TotalSeconds * (i + 1))
					);
				emaData.Add(dataUnit);
				logger.Info($"Predicted value for day {i + 1}: {dataUnit.valueData} at {dataUnit.timeData}");
			}

			return emaData;
		}
	}
}
