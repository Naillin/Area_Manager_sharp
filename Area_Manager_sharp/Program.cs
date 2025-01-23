using Area_Manager_sharp;
using Area_Manager_sharp.DBTools;
using Area_Manager_sharp.ElevationAnalyzer;
using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.MovingAverage;
using NLog;
using NLog.Fluent;
using System.Text.Json;

class Program //Убрать Console.Writeline
{
	private static readonly string moduleName = "area-manager-sharp.main";
	private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
	private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

	public static int ROUND_DIGITS = 6;
	private static double DISTANCE = 200;
	private static int DELAY = 300;
	private static double WINDOW_SIZE = 7;
	private static double SMOOTHING = 10;
	private static double SLOPE_FACTOR = 3;
	private static bool EQUAL_OPTION = false;
	private static bool USE_INFLUX = false;
	private static bool DEBUG_MODE = false;
	public static string SQL_CONNECTION = "Data Source=../MQTT_Data_collector/mqtt_data.db";
	//private string SQL_CONNECTION = "Data Source=mqtt_data.db";
	private const string filePathConfig = "config.txt";

	private static string configTextDefault = string.Empty;
	private static void initConfig()
	{
		if (File.Exists(filePathConfig))
		{
			string[] linesConfig = File.ReadAllLines(filePathConfig);
			ROUND_DIGITS = Convert.ToInt32(linesConfig[0].Split(':')[1]);
			DISTANCE = Convert.ToDouble(linesConfig[1].Split(':')[1]);
			DELAY = Convert.ToInt32(linesConfig[2].Split(':')[1]);
			WINDOW_SIZE = Convert.ToDouble(linesConfig[3].Split(':')[1]);
			SMOOTHING = Convert.ToDouble(linesConfig[4].Split(':')[1]);
			SLOPE_FACTOR = Convert.ToDouble(linesConfig[5].Split(':')[1]);
			EQUAL_OPTION = bool.Parse(linesConfig[6].Split(':')[1]);
			USE_INFLUX = bool.Parse(linesConfig[7].Split(':')[1]);
			DEBUG_MODE = bool.Parse(linesConfig[8].Split(':')[1]);
			SQL_CONNECTION = linesConfig[9].Split(':')[1];

			configTextDefault = $"ROUND_DIGITS:{ROUND_DIGITS}\r\n" +
								$"DISTANCE:{DISTANCE}\r\n" +
								$"DELAY:{DELAY}\r\n" +
								$"WINDOW_SIZE:{WINDOW_SIZE}\r\n" +
								$"SMOOTHING:{SMOOTHING}\r\n" +
								$"SLOPE_FACTOR:{SLOPE_FACTOR}\r\n" +
								$"EQUAL_OPTION:{EQUAL_OPTION}\r\n" +
								$"USE_INFLUX:{USE_INFLUX}\r\n" +
								$"DEBUG_MODE:{DEBUG_MODE}\r\n" +
								$"SQL_CONNECTION:{SQL_CONNECTION}";
		}
		else
		{
			configTextDefault = $"ROUND_DIGITS:{ROUND_DIGITS}\r\n" +
								$"DISTANCE:{DISTANCE}\r\n" +
								$"DELAY:{DELAY}\r\n" +
								$"WINDOW_SIZE:{WINDOW_SIZE}\r\n" +
								$"SMOOTHING:{SMOOTHING}\r\n" +
								$"SLOPE_FACTOR:{SLOPE_FACTOR}\r\n" +
								$"EQUAL_OPTION:{EQUAL_OPTION}\r\n" +
								$"USE_INFLUX:{USE_INFLUX}\r\n" +
								$"DEBUG_MODE:{DEBUG_MODE}\r\n" +
								$"SQL_CONNECTION:{SQL_CONNECTION}";
			File.WriteAllText(filePathConfig, configTextDefault);
		}
	}

	private static int _topicID = 0;
	public static int TopicID { get { return _topicID; } }
	static async Task Main(string[] args)
	{
		// Определение системы
		//bool isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
		//GlobalDiagnosticsContext.Set("isLinux", isLinux.ToString().ToLower()); // Передача переменной в NLog

		logger.Info($"Starting...");
		Console.WriteLine($"Starting...");

		Program program = new Program();
		Program.initConfig();

		logger.Info(Program.configTextDefault);
		Console.WriteLine(Program.configTextDefault);

		DBTools dBTools = new DBTools(Program.SQL_CONNECTION);
		dBTools.journalMode("WAL");

		ElevationAnalyzer analyzer = new ElevationAnalyzer(Program.DELAY, 3, Program.DEBUG_MODE);

		// Глобальный словарь для хранения времени последнего изменения данных для каждого топика
		Dictionary<int, long?> lastDataChange = new Dictionary<int, long?>();

		logger.Info($"All done!");
		Console.WriteLine($"All done!");

		while (true)
		{
			object?[,] topics = dBTools.executeSelectTable($"SELECT ID_Topic, Latitude_Topic, Longitude_Topic, CheckTime_Topic FROM Topics;");
			logger.Info($"Topics count: {topics.GetLength(0)}");
			Console.WriteLine($"Topics count: {topics.GetLength(0)}");
			for (int i = 0; i < topics.GetLength(0); i++)
			{
				_topicID = Convert.ToInt32(topics[i, 0]);
				double latitude = Convert.ToDouble(topics[i, 1]);
				double longitude = Convert.ToDouble(topics[i, 2]);
				object? checkTime = topics[i, 3];
				DateTime checkTimeDT = DateTime.Now;
				if (checkTime != null)
				{
					checkTimeDT = DateTimeOffset.FromUnixTimeSeconds((long)checkTime).DateTime;
				}
				logger.Info($"Checking topic {_topicID}");

				// Если расчет был 2 часа назад, то нужно повторить проверку по параметрам затопления
				if (checkTime == null || ((DateTime.Now - checkTimeDT).TotalHours >= 2))
				{
					dBTools.journalMode("WAL");
					object? latestDataTime = dBTools.executeAnySqlScalar($"SELECT MAX(Time_Data) FROM Data WHERE ID_Topic = {_topicID};");

					// Если есть новые данные, или это первый расчет для топика
					if (!lastDataChange.TryGetValue(_topicID, out long? value) || latestDataTime == null || ((long?)latestDataTime > value))
					{
						lastDataChange[_topicID] = (long?)latestDataTime;
						double prediction3 = program.checkTopicConditions(_topicID);

						// Если данные прошли проверку по параметрам затопления, то топику угрожает затопление. Рассчет области затопления.
						if (prediction3 != -1)
						{
							logger.Info($"Conditions met for topic {_topicID}. Calculating area points.");
							// Вычисялем точки
							PointsPack pointsPack = await analyzer.findArea(new Сoordinate(latitude, longitude), prediction3, Program.DISTANCE, Program.EQUAL_OPTION, Program.USE_INFLUX);

							if (program.insertAreaData(pointsPack, _topicID, true, true) == -1)
							{
								logger.Warn($"Topic {_topicID} does not exist in the database or was deleted. Skipping operations.");
								continue;
							}
						}
						else
						{
							// Если данные не прошли проверку по параметрам затопления, то топику не угрожает затопление. Очистка данных области затопления. Обновляем CheckTime_Topic, чтобы отметить, что топик был проверен.
							logger.Info($"Conditions not met for topic {_topicID}. Clearing data from AreaPoints.");
							dBTools.journalMode("WAL");
							dBTools.executeDelete("AreaPoints", $"where ID_Topic = {_topicID}");
							dBTools.executeUpdate("Topics", $"CheckTime_Topic = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", $"where ID_Topic = {_topicID}");
							logger.Info($"Data for topic {_topicID} cleared from AreaPoints and CheckTime_Topic updated.");
							Console.WriteLine();
						}
					}
					else
					{
						// Если новых данных нет, то расчет не требуется, но обновляем CheckTime_Topic, чтобы отметить, что топик был проверен.
						logger.Info($"No new data for topic {_topicID} since last calculation. Updating CheckTime_Topic.");
						dBTools.journalMode("WAL");
						dBTools.executeUpdate("Topics", $"CheckTime_Topic = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", $"where ID_Topic = {_topicID}");
						logger.Info($"CheckTime_Topic updated for topic {_topicID}.");
						Console.WriteLine();
					}
				}
				else
				{
					// Если топик был проверен менее 2 часов назад, то пропускаем расчет.
					logger.Info($"Topic {_topicID} was recently checked. Skipping calculation.");
				}
			}

			logger.Info($"Pause time.");
			Console.WriteLine($"Pause time.");
			await Task.Delay(60000);
		}
	}

	private double checkTopicConditions(int _topicID)
	{
		double result = -1;

		DBTools dBTools = new DBTools(SQL_CONNECTION);
		dBTools.journalMode("WAL");
		object? altitudeObj = dBTools.executeAnySqlScalar($"SELECT Altitude_Topic FROM Topics WHERE ID_Topic = {_topicID};");

		if (altitudeObj != null)
		{
			double altitude = (double)altitudeObj;
			logger.Info($"Altitude for topic {_topicID}: {altitude}");

			object?[,] dataObj = dBTools.executeSelectTable($"SELECT Value_Data, Time_Data FROM Data WHERE ID_Topic = {_topicID} ORDER BY Time_Data ASC;");
			List<DataUnit> data = Enumerable.Range(0, dataObj.GetLength(0))
				.Select(i => new DataUnit(
					Convert.ToDouble(dataObj[i, 0]), // Value_Data
					Convert.ToInt64(dataObj[i, 1]) // Time_Data
				)).ToList();

			if (data.Count > 2)
			{
				logger.Info($"Data for topic {_topicID}: {string.Join(";", data)}");

				MovingAverage movingAverage = new MovingAverage(WINDOW_SIZE);
				List<DataUnit> predictedEvents = movingAverage.CalculateEmaSmooth(data, SMOOTHING, SLOPE_FACTOR);
				if (predictedEvents.Count >= 10)
				{
					List<DataUnit> lastPredict = predictedEvents.TakeLast(3).ToList();
					List<DataUnit> lastFact = data.TakeLast(2).ToList();
					logger.Info($"Predicted values for topic {_topicID}: p1={lastPredict[0]}, p2={lastPredict[1]}, p3={lastPredict[2]}");
					logger.Info($"Actual values for topic {_topicID}: f1={lastFact[0]}, f2={lastFact[1]}");

					if (lastPredict[2].valueData > altitude && lastFact[1].valueData > lastFact[0].valueData)
					{
						result = Convert.ToDouble(lastPredict[2].valueData);
						logger.Info($"Conditions met for topic {_topicID}: p3={lastPredict[2].valueData} > alt={altitude} and f1={lastFact[0].valueData} > f2={lastFact[1].valueData}");
					}
					else
					{
						result = -1;
						logger.Info($"Conditions not met for topic {_topicID}: p3={lastPredict[2].valueData} > alt={altitude} and f1={lastFact[0].valueData} > f2={lastFact[1].valueData}");
					}
				}
				else
				{
					result = -1;
					logger.Warn($"Not enough data to predict for topic {_topicID}.");
				}
			}
			else
			{
				result = -1;
				logger.Warn($"No data found for topic {_topicID}.");
			}
		}
		else
		{
			result = -1;
			logger.Warn($"Topic with ID {_topicID} not found.");
		}

		return result;
	}

	public int insertAreaData (PointsPack pointsPack, int _topicID, bool deleteOld = true, bool updateTime = true)
	{
		int result = -1;

		string depressionPoints = string.Join(", ", pointsPack.DepressionPoints);
		string perimeterPoints = string.Join(", ", pointsPack.PerimeterPoints);
		string includedPoints = string.Join(", ", pointsPack.IncludedPoints);
		//string islands = JsonSerializer.Serialize(pointsPack.Islands, new JsonSerializerOptions { WriteIndented = true }); //пока не нужно 
		string islands = "[]";

		DBTools dBTools = new DBTools(SQL_CONNECTION);
		dBTools.journalMode("WAL");
		int countTopic = dBTools.countRows("Topics", $"where ID_Topic = {_topicID}");
		if (countTopic > 0)
		{
			if (deleteOld)
			{
				dBTools.executeDelete("AreaPoints", $"where ID_Topic = {_topicID}");
				dBTools.executeInsert("AreaPoints", [_topicID.ToString(), $"[{depressionPoints}]", $"[{perimeterPoints}]", $"[{includedPoints}]", $"[{islands}]"]);
			}
			else
			{
				object?[,] data = dBTools.executeSelectTable($"select * from AreaPoints where ID_Topic = {_topicID};");
				string depressionPointsNew = data[0, 2]?.ToString() + " " + depressionPoints;
				string perimeterPointsNew = data[0, 3]?.ToString() + " " + perimeterPoints;
				string includedPointsNew = data[0, 4]?.ToString() + " " + includedPoints;
				List<Island>? islandsList = JsonSerializer.Deserialize<List<Island>>(data[0, 5]?.ToString());
				if (islandsList != null)
				{
					pointsPack.Islands.AddRange(islandsList);
					//islands = JsonSerializer.Serialize(islandsList, new JsonSerializerOptions { WriteIndented = true });
				}

				dBTools.executeUpdate("AreaPoints", [_topicID.ToString(), depressionPoints, perimeterPoints, includedPoints, islands]);
			}
			
			if (updateTime)
			{
				dBTools.executeUpdate("Topics", $"CheckTime_Topic = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", $"where ID_Topic = {_topicID}");
			}
			logger.Info($"Data for topic {_topicID} inserted into AreaPoints and CheckTime_Topic: updated = {updateTime}.");

			result = 1;
		}
		else
		{
			result = -1;
		}

		return result;
	}
}