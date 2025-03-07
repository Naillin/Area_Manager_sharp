using Area_Manager_sharp.MovingAverage;

namespace Area_Manager_sharp.MovingAverageFolder
{
	internal class Metrics
	{
		public static double CalculateMAE(List<DataUnit> actual, List<DataUnit> predicted)
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

		public static double CalculateMSE(List<DataUnit> actual, List<DataUnit> predicted)
		{
			// Очищаем списки от null в начале и лишних элементов в конце
			var cleanedData = CleanData(actual, predicted);
			var cleanedActual = cleanedData.actual;
			var cleanedPredicted = cleanedData.predicted;

			// Вычисляем MSE
			double sum = 0;
			int count = 0;

			for (int i = 0; i < cleanedActual.Count; i++)
			{
				if (cleanedActual[i].valueData.HasValue && cleanedPredicted[i].valueData.HasValue)
				{
					sum += Math.Pow(cleanedActual[i].valueData.Value - cleanedPredicted[i].valueData.Value, 2);
					count++;
				}
			}

			if (count == 0)
				throw new InvalidOperationException("Нет данных для вычисления MSE.");

			return sum / count;
		}

		public static double CalculateR2(List<DataUnit> actual, List<DataUnit> predicted)
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

		// Метод для очистки данных
		private static (List<DataUnit> actual, List<DataUnit> predicted) CleanData(List<DataUnit> actual, List<DataUnit> predicted)
		{
			// Удаляем null в начале predicted
			int nullCount = predicted.TakeWhile(p => p.valueData == null).Count();
			var cleanedPredicted = predicted.Skip(nullCount).ToList();

			// Удаляем первые nullCount элементов из actual
			var cleanedActual = actual.Skip(nullCount).ToList();

			// Удаляем последние 3 элемента из predicted
			cleanedPredicted = cleanedPredicted.Take(cleanedPredicted.Count - 3).ToList();

			return (cleanedActual, cleanedPredicted);
		}
	}
}
