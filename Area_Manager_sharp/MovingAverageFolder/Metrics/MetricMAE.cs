using Area_Manager_sharp.MovingAverage;

namespace Area_Manager_sharp.MovingAverageFolder.Metrics
{
	internal class MetricMAE : Metric
	{
		public override double Calculate(List<DataUnit> actual, List<DataUnit> predicted)
		{
			// Очищаем списки от null в начале и лишних элементов в конце
			var cleanedData = CleanData(actual, predicted);
			var cleanedActual = cleanedData.actual;
			var cleanedPredicted = cleanedData.predicted;

			// Вычисляем MAE
			double sum = 0;
			int count = 0;

			for (int i = 0; i < cleanedActual.Count; i++)
			{
				if (cleanedActual[i].valueData.HasValue && cleanedPredicted[i].valueData.HasValue)
				{
					sum += Math.Abs(cleanedActual[i].valueData.Value - cleanedPredicted[i].valueData.Value);
					count++;
				}
			}

			if (count == 0)
				throw new InvalidOperationException("Нет данных для вычисления MAE.");

			return sum / count;
		}
	}
}
