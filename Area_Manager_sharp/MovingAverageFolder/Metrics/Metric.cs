using Area_Manager_sharp.MovingAverage;

namespace Area_Manager_sharp.MovingAverageFolder.Metrics
{
	internal abstract class Metric : IMetric
	{
		protected (List<DataUnit> actual, List<DataUnit> predicted) CleanData(List<DataUnit> actual, List<DataUnit> predicted)
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

		public abstract double Calculate(List<DataUnit> actual, List<DataUnit> predicted);
	}
}
