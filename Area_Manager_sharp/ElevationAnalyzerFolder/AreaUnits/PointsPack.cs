using Area_Manager_sharp.ElevationAnalyzer;

namespace Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits
{
	internal class PointsPack
	{
		public List<Coordinate> DepressionPoints = new List<Coordinate>();
		public List<Coordinate> PerimeterPoints = new List<Coordinate>();
		public List<Coordinate> IncludedPoints = new List<Coordinate>();
		public List<Island> Islands = new List<Island>();

		public PointsPack (List<Coordinate> DepressionPoints, List<Coordinate> PerimeterPoints, List<Coordinate> IncludedPoints, List<Island> Islands)
		{
			this.DepressionPoints = DepressionPoints;
			this.PerimeterPoints = PerimeterPoints;
			this.IncludedPoints = IncludedPoints;
			this.Islands = Islands;
		}
	}
}
