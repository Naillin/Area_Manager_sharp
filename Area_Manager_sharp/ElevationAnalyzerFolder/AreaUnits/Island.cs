using Area_Manager_sharp.ElevationAnalyzer;

namespace Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits
{
	internal class Island
	{
		public int ID { get; set; }
		public List<Coordinate> Coords = new List<Coordinate>();

		public Island(int ID, List<Coordinate> Coords)
		{
			this.ID = ID;
			this.Coords = Coords;
		}

		public override string ToString()
		{
			return $"ID: {ID}, Coords: [{string.Join(", ", Coords)}]";
		}
	}
}
