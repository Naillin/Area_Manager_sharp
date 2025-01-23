using Area_Manager_sharp.ElevationAnalyzer;

namespace Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits
{
	internal class Island
	{
		public int ID { get; set; }
		public List<Сoordinate> Coords = new List<Сoordinate>();

		public Island(int ID, List<Сoordinate> Coords)
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
