using Area_Manager_sharp.MovingAverage;

namespace Area_Manager_sharp.MovingAverageFolder.Metrics
{
	internal interface IMetric
	{
		public abstract double Calculate(List<DataUnit> actual, List<DataUnit> predicted);
	}
}
