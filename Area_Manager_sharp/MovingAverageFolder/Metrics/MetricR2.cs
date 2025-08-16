using Area_Manager_sharp.MovingAverage;

namespace Area_Manager_sharp.MovingAverageFolder.Metrics
{
	internal class MetricR2 : Metric
	{
		public override double Calculate(List<DataUnit> actual, List<DataUnit> predicted)
		{
			// Очищаем списки от null в начале и лишних элементов в конце
			var cleanedData = CleanData(actual, predicted);
			var cleanedActual = cleanedData.actual;
			var cleanedPredicted = cleanedData.predicted;

			// Вычисляем среднее значение actual (игнорируя null)
			double meanActual = cleanedActual.Where(a => a.valueData.HasValue).Average(a => a.valueData.Value);

			// Вычисляем общую сумму квадратов (SS Total)
			double ssTotal = cleanedActual.Where(a => a.valueData.HasValue)
										  .Sum(a => Math.Pow(a.valueData.Value - meanActual, 2));

			// Вычисляем сумму квадратов ошибок (SS Residual)
			double ssResidual = 0;
			int count = 0;

			for (int i = 0; i < cleanedActual.Count; i++)
			{
				if (cleanedActual[i].valueData.HasValue && cleanedPredicted[i].valueData.HasValue)
				{
					ssResidual += Math.Pow(cleanedActual[i].valueData.Value - cleanedPredicted[i].valueData.Value, 2);
					count++;
				}
			}

			if (count == 0)
				throw new InvalidOperationException("Нет данных для вычисления R².");

			return 1 - (ssResidual / ssTotal);
		}
	}
}
