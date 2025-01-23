using Area_Manager_sharp.ElevationAnalyzer;

namespace Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits
{
	internal class PointsPack
	{
		public List<Сoordinate> DepressionPoints = new List<Сoordinate>();
		public List<Сoordinate> PerimeterPoints = new List<Сoordinate>();
		public List<Сoordinate> IncludedPoints = new List<Сoordinate>();
		public List<Island> Islands = new List<Island>();

		public PointsPack (List<Сoordinate> DepressionPoints, List<Сoordinate> PerimeterPoints, List<Сoordinate> IncludedPoints, List<Island> Islands)
		{
			this.DepressionPoints = DepressionPoints;
			this.PerimeterPoints = PerimeterPoints;
			this.IncludedPoints = IncludedPoints;
			this.Islands = Islands;
		}
	}
}
