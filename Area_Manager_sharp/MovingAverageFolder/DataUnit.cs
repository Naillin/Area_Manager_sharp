
namespace Area_Manager_sharp.MovingAverage
{
	internal class DataUnit
	{
		public double? valueData;
		public long timeData;

		public DataUnit(double? valueData, long timeData)
		{
			this.valueData = valueData;
			this.timeData = timeData;
		}

		public override string ToString()
		{
			return $"{valueData};{timeData}";
		}
	}
}
