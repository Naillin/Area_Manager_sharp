
namespace Area_Manager_sharp.ElevationAnalyzer
{
	internal class Coordinate
	{
		private int roundDigits = Program.ROUND_DIGITS;

		private double _latitude;
		private double _longitude;

		public double Latitude
		{
			get
			{
				return _latitude;
			}
			set
			{
				_latitude = Math.Round(value, roundDigits);
			}
		}
		public double Longitude
		{
			get
			{
				return _longitude;
			}
			set
			{
				_longitude = Math.Round(value, roundDigits);
			}
		}

		public Coordinate(double latitude, double longitude)
		{
			this.Latitude = latitude;
			this.Longitude = longitude;
		}

		public override string ToString()
		{
			return $"[{Latitude}, {Longitude}]";
		}

		public override bool Equals(object? obj)
		{
			if (obj is null) // Проверка на null
				return false;

			if (obj is Coordinate other) // Проверка типа
			{
				return Latitude == other.Latitude && Longitude == other.Longitude;
			}

			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Latitude, Longitude);
		}
	}
}
