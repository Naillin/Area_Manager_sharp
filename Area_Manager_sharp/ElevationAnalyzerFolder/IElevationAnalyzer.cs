using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;

namespace Area_Manager_sharp.ElevationAnalyzerFolder
{
	internal interface IElevationAnalyzer
	{
		public abstract PointsPack FindArea(Coordinate coordinate, double initialHeight);
	}
}
