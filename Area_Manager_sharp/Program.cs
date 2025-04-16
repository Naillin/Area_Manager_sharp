using Area_Manager_sharp;
using Area_Manager_sharp.DBTools;
using Area_Manager_sharp.ElevationAnalyzer;
using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.MovingAverage;
using IniParser.Model;
using IniParser;
using NLog;
using NLog.Fluent;
using System.Text.Json;
using Area_Manager_sharp.MovingAverageFolder;

class Program
{
	private static readonly string moduleName = "Program";
	private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
	private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

	public static int ROUND_DIGITS = 6;
	private static bool DEBUG_MODE = false;
	private static string SQL_CONNECTION = "Data Source=../MQTT_Data_collector/mqtt_data.db";
	private static int COUNT_DATA = 100;
	//private static string SQL_CONNECTION = "Data Source=mqtt_data.db";

	private static double WINDOW_SIZE = 7;
	private static double SMOOTHING = 2;
	private static double SLOPE_FACTOR = 2;
	private static double ADD_NUMBER = 0.5;
	
	private static int DELAY = 300;
	private static double DISTANCE = 200;
	private static bool EQUAL_OPTION = false;
	private static bool USE_INFLUX = false;
	private static int USE_SRTM = 0;
	private static double RADIUS = 10000;
	private static int COUNT_OF_SUBS = 100;
	private static double COEF_HEIGHT = 2.0;
	public static string TILES_FOLDER = "tilesFolder/";
	//public static string TILES_FOLDER = "C:/Users/kamil/Desktop/tiles/";
	public static string GDAL_PYTHON = "GDALPython/venv/bin/python3";
	public static string GDAL_PYSCRIPT = "GDALPython/main.py";

	private const string filePathConfig = "config.ini";
	private static string configTextDefault = string.Empty;
	private static void initConfig()
	{
		FileIniDataParser parser = new FileIniDataParser();

		if (File.Exists(filePathConfig))
		{
			logger.Info($"Чтение конфигурационного файла.");

			IniData data = parser.ReadFile(filePathConfig);
			ROUND_DIGITS = Convert.ToInt32(data["Settings"]["ROUND_DIGITS"]);
			DEBUG_MODE = bool.Parse(data["Settings"]["DEBUG_MODE"]);
			SQL_CONNECTION = data["Settings"]["SQL_CONNECTION"];
			COUNT_DATA = Convert.ToInt32(data["Settings"]["COUNT_DATA"]);

			WINDOW_SIZE = Convert.ToDouble(data["MovingAverage"]["WINDOW_SIZE"]);
			SMOOTHING = Convert.ToDouble(data["MovingAverage"]["SMOOTHING"]);
			SLOPE_FACTOR = Convert.ToDouble(data["MovingAverage"]["SLOPE_FACTOR"]);
			ADD_NUMBER = Convert.ToDouble(data["MovingAverage"]["ADD_NUMBER"]);

			DELAY = Convert.ToInt32(data["ElevationAnalyzer"]["DELAY"]);
			DISTANCE = Convert.ToDouble(data["ElevationAnalyzer"]["DISTANCE"]);
			EQUAL_OPTION = bool.Parse(data["ElevationAnalyzer"]["EQUAL_OPTION"]);
			USE_INFLUX = bool.Parse(data["ElevationAnalyzer"]["USE_INFLUX"]);
			USE_SRTM = Convert.ToInt32(data["ElevationAnalyzer"]["USE_SRTM"]);
			RADIUS = Convert.ToDouble(data["ElevationAnalyzer"]["RADIUS"]);
			COUNT_OF_SUBS = Convert.ToInt32(data["ElevationAnalyzer"]["COUNT_OF_SUBS"]);
			COEF_HEIGHT = Convert.ToDouble(data["ElevationAnalyzer"]["COEF_HEIGHT"]);
			TILES_FOLDER = data["ElevationAnalyzer"]["TILES_FOLDER"];
			GDAL_PYTHON = data["ElevationAnalyzer"]["GDAL_PYTHON"];
			GDAL_PYSCRIPT = data["ElevationAnalyzer"]["GDAL_PYSCRIPT"];
		}
		else
		{
			logger.Info($"Создание конфигурационного файла.");

			IniData data = new IniData();
			data.Sections.AddSection("Settings");
			data["Settings"]["ROUND_DIGITS"] = ROUND_DIGITS.ToString();
			data["Settings"]["DEBUG_MODE"] = DEBUG_MODE.ToString();
			data["Settings"]["SQL_CONNECTION"] = SQL_CONNECTION.ToString();
			data["Settings"]["COUNT_DATA"] = COUNT_DATA.ToString();

			data.Sections.AddSection("MovingAverage");
			data["MovingAverage"]["WINDOW_SIZE"] = WINDOW_SIZE.ToString();
			data["MovingAverage"]["SMOOTHING"] = SMOOTHING.ToString();
			data["MovingAverage"]["SLOPE_FACTOR"] = SLOPE_FACTOR.ToString();
			data["MovingAverage"]["ADD_NUMBER"] = ADD_NUMBER.ToString();

			data.Sections.AddSection("ElevationAnalyzer");
			data["ElevationAnalyzer"]["DELAY"] = DELAY.ToString();
			data["ElevationAnalyzer"]["DISTANCE"] = DISTANCE.ToString();
			data["ElevationAnalyzer"]["EQUAL_OPTION"] = EQUAL_OPTION.ToString();
			data["ElevationAnalyzer"]["USE_INFLUX"] = USE_INFLUX.ToString();
			data["ElevationAnalyzer"]["USE_SRTM"] = USE_SRTM.ToString();
			data["ElevationAnalyzer"]["RADIUS"] = RADIUS.ToString();
			data["ElevationAnalyzer"]["COUNT_OF_SUBS"] = COUNT_OF_SUBS.ToString();
			data["ElevationAnalyzer"]["COEF_HEIGHT"] = COEF_HEIGHT.ToString();
			data["ElevationAnalyzer"]["TILES_FOLDER"] = TILES_FOLDER.ToString();
			data["ElevationAnalyzer"]["GDAL_PYTHON"] = GDAL_PYTHON.ToString();
			data["ElevationAnalyzer"]["GDAL_PYSCRIPT"] = GDAL_PYSCRIPT.ToString();

			parser.WriteFile(filePathConfig, data);
		}

		configTextDefault = $"ROUND_DIGITS = [{ROUND_DIGITS}]\n" +
							$"DEBUG_MODE = [{DEBUG_MODE}]\n" +
							$"SQL_CONNECTION = [{SQL_CONNECTION}]\n" +
							$"COUNT_DATA = [{COUNT_DATA}]\n" +
							
							$"WINDOW_SIZE = [{WINDOW_SIZE}]\n" +
							$"SMOOTHING = [{SMOOTHING}]\n" +
							$"SLOPE_FACTOR = [{SLOPE_FACTOR}]\n" +
							$"ADD_NUMBER = [{ADD_NUMBER}]\n" +

							$"DELAY = [{DELAY}]\n" +
							$"DISTANCE = [{DISTANCE}]\n" +
							$"EQUAL_OPTION = [{EQUAL_OPTION}]\n" +
							$"USE_INFLUX = [{USE_INFLUX}]\n" +
							$"USE_SRTM = [{USE_SRTM}]\n" +
							$"RADIUS = [{RADIUS}]\n" +
							$"COUNT_OF_SUBS = [{COUNT_OF_SUBS}]\n" +
							$"COEF_HEIGHT = [{COEF_HEIGHT}]\n" +
							$"TILES_FOLDER = [{TILES_FOLDER}]\n" +
							$"GDAL_PYTHON = [{GDAL_PYTHON}]\n" +
							$"GDAL_PYSCRIPT = [{GDAL_PYSCRIPT}]";
	}

	private static int _topicID = 0;
	public static int TopicID { get { return _topicID; } }
	static async Task Main(string[] args)
	{
		// Определение системы
		//bool isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
		//GlobalDiagnosticsContext.Set("isLinux", isLinux.ToString().ToLower()); // Передача переменной в NLog

		logger.Info($"Starting...");
		await Task.Delay(3000);
		Program program = new Program();
		Program.initConfig();

		logger.Info(Program.configTextDefault);

		DBTools dBTools = new DBTools(Program.SQL_CONNECTION);
		dBTools.journalMode("WAL");

		ElevationAnalyzer analyzer = new ElevationAnalyzer(Program.DELAY, 3, Program.DISTANCE, Program.EQUAL_OPTION, Program.DEBUG_MODE);

		// Глобальный словарь для хранения времени последнего изменения данных для каждого топика
		Dictionary<int, long?> lastDataChange = new Dictionary<int, long?>();

		logger.Info($"All done!");
		await Task.Delay(3000);

		while (true)
		{
			object?[,] topics = dBTools.executeSelectTable($"SELECT ID_Topic, Latitude_Topic, Longitude_Topic, CheckTime_Topic FROM Topics;");
			logger.Info($"Topics count: {topics.GetLength(0)}");
			for (int i = 0; i < topics.GetLength(0); i++)
			{
				_topicID = Convert.ToInt32(topics[i, 0]);
				double latitude = Convert.ToDouble(topics[i, 1]);
				double longitude = Convert.ToDouble(topics[i, 2]);
				object? checkTime = topics[i, 3];
				DateTime checkTimeDT = DateTime.UtcNow;
				if (checkTime != null && long.TryParse(checkTime.ToString(), out long unixTime))
				{
					checkTimeDT = DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
				}
				else
				{
					// Если checkTime равен null или не может быть преобразован в long, используем текущее время
					checkTimeDT = DateTime.UtcNow;
				}
				logger.Info($"Checking topic {_topicID}. checkTime = {checkTimeDT}.");

				// Если расчет был 2 часа назад, то нужно повторить проверку по параметрам затопления
				if (checkTime == null || ((DateTime.UtcNow - checkTimeDT).TotalHours > 2))
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
							PointsPack pointsPack;
							switch (USE_SRTM)
							{
								case 0:
									pointsPack = analyzer.findAreaGDAL(new Coordinate(latitude, longitude), prediction3, Program.USE_INFLUX);
									break;
								case 1:
									pointsPack = analyzer.findAreaFigureGDAL(new Coordinate(latitude, longitude), prediction3, Program.RADIUS, Program.COUNT_OF_SUBS, Program.COEF_HEIGHT);
									break;
								default:
									pointsPack = await analyzer.findArea(new Coordinate(latitude, longitude), prediction3, Program.USE_INFLUX);
									break;
							}
							
							// Если по каким то причинам не получилось вставить данные
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
						}
					}
					else
					{
						// Если новых данных нет, то расчет не требуется, но обновляем CheckTime_Topic, чтобы отметить, что топик был проверен.
						logger.Info($"No new data for topic {_topicID} since last calculation. Updating CheckTime_Topic.");
						dBTools.journalMode("WAL");
						dBTools.executeUpdate("Topics", $"CheckTime_Topic = {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", $"where ID_Topic = {_topicID}");
						logger.Info($"CheckTime_Topic updated for topic {_topicID}.");
					}
				}
				else
				{
					// Если топик был проверен менее 2 часов назад, то пропускаем расчет.
					logger.Info($"Topic {_topicID} was recently checked. Skipping calculation.");
				}
			}

			logger.Info($"Pause time.");
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

			object?[,] dataObj = dBTools.executeSelectTable($"SELECT Value_Data, Time_Data FROM Data WHERE ID_Topic = {_topicID} ORDER BY Time_Data DESC LIMIT {Program.COUNT_DATA};");
			List<DataUnit> data = Enumerable.Range(0, dataObj.GetLength(0))
				.Select(i => new DataUnit(
					Convert.ToDouble(dataObj[i, 0]), // Value_Data
					Convert.ToInt64(dataObj[i, 1]) // Time_Data
				)).Reverse().ToList();

			if (data.Count > 2)
			{
				logger.Info($"Data for topic {_topicID}: {string.Join(";", data)}");

				MovingAverage movingAverage = new MovingAverage(WINDOW_SIZE);
				List<DataUnit> predictedEvents = movingAverage.CalculateEmaSmooth(data, SMOOTHING, SLOPE_FACTOR);
				if (predictedEvents.Count >= 10)
				{
					List<DataUnit> lastPredict = predictedEvents.TakeLast(4).ToList();
					List<DataUnit> lastFact = data.TakeLast(2).ToList();
					logger.Info($"Predicted values for topic {_topicID}: p0 = {lastPredict[0].valueData}, p1 = {lastPredict[1].valueData}, p2 = {lastPredict[2].valueData}, p3 = {lastPredict[3].valueData}.");
					logger.Info($"Actual values for topic {_topicID}: f1 = {lastFact[0].valueData}, f2 = {lastFact[1].valueData}.");

					double metric = Metrics.CalculateMAE(data, predictedEvents);
					double p3baff = Convert.ToDouble(lastPredict[3].valueData ?? 0.0) + metric;
					double? buffNumber = lastPredict[0].valueData + ADD_NUMBER;
					logger.Info($"Metric = {metric}, p3_buffed = {p3baff}, buffNumber = {buffNumber}");
					//if (lastPredict[2].valueData > altitude && lastFact[1].valueData > lastFact[0].valueData)
					if (lastFact[1].valueData >= buffNumber && p3baff >= altitude) //F_last > (E_last + buffNumber) & (predict3 + MAE) >= height 
					{
						result = Convert.ToDouble(lastPredict[3].valueData);
						logger.Info($"Conditions met for topic {_topicID}: f1 = {lastFact[1].valueData} >= buffNumber = {buffNumber} and p3_buffed = {p3baff} >= altitude = {altitude}.");
					}
					else
					{
						result = -1;
						logger.Info($"Conditions not met for topic {_topicID}: f1 = {lastFact[1].valueData} >= buffNumber = {buffNumber} and p3_buffed = {p3baff} >= altitude = {altitude}.");
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
		string islands = string.Empty;

		DBTools dBTools = new DBTools(SQL_CONNECTION);
		dBTools.journalMode("WAL");
		int countTopic = dBTools.countRows("Topics", $"where ID_Topic = {_topicID}");
		// Если запись не удалена на момент вставки
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
				string depressionPointsBuffer = data[0, 2]?.ToString() ?? string.Empty;
				string perimeterPointsBuffer = data[0, 3]?.ToString() ?? string.Empty;
				string includedPointsBuffer = data[0, 4]?.ToString() ?? string.Empty;
				string islandsStr = data[0, 5]?.ToString() ?? string.Empty;

				string depressionPointsNew = string.Empty;
				if (!string.IsNullOrEmpty(depressionPointsBuffer))
				{ depressionPointsNew = depressionPointsBuffer.Substring(1, depressionPointsBuffer.Length - 2) + ", " + depressionPoints; }
				string perimeterPointsNew = string.Empty;
				if (!string.IsNullOrEmpty(perimeterPointsBuffer))
				{ perimeterPointsNew = perimeterPointsBuffer.Substring(1, perimeterPointsBuffer.Length - 2) + ", " + perimeterPoints; }
				string includedPointsNew = string.Empty;
				if (!string.IsNullOrEmpty(includedPointsBuffer))
				{ includedPointsNew = includedPointsBuffer.Substring(1, includedPointsBuffer.Length - 2) + ", " + includedPoints; }
				
				string islandsNew = string.Empty;
				if (!string.IsNullOrEmpty(islandsStr))
				{
					List<Island>? islandsList = JsonSerializer.Deserialize<List<Island>>(islandsStr);
					if (islandsList != null)
					{
						pointsPack.Islands.AddRange(islandsList);
						//islandsNew = JsonSerializer.Serialize(islandsList, new JsonSerializerOptions { WriteIndented = true });
					}
				}

				dBTools.executeUpdate("AreaPoints", [$"[{depressionPointsNew}]", $"[{perimeterPointsNew}]", $"[{includedPointsNew}]", $"[{islandsNew}]"], $"where ID_Topic = {_topicID}");
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
			// Если запись удалена на момент вставки
			result = -1;
		}
		
		return result;
	}
}