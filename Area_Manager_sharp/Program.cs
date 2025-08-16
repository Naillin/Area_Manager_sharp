using Area_Manager_sharp;
using Area_Manager_sharp.DBTools;
using Area_Manager_sharp.DBTools.DataProvider;
using Area_Manager_sharp.ElevationAnalyzerFolder;
using Area_Manager_sharp.ElevationAnalyzerFolder.AreaUnits;
using Area_Manager_sharp.MovingAverage;
using Area_Manager_sharp.MovingAverageFolder.Metrics;
using Area_Manager_sharp.Queries;
using Area_Manager_sharp.Queries.API;
using IniParser;
using IniParser.Model;
using NLog;
using System.Globalization;

class Program
{
	private static readonly string moduleName = "Program";
	private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
	private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

	public static int ROUND_DIGITS = 6;
	public static bool DEBUG_MODE = false;
	public static string SQL_CONNECTION = "Data Source=../MQTT_Data_collector/mqtt_data.db";
	public static int COUNT_DATA = 100;
	public static int CONNECTION_METHOD = 0;
	public static string API_URL_CONNECTION = "127.0.0.1:8080";
	public static string API_LOGIN_CONNECTION = string.Empty;
	public static string API_PASSWORD_CONNECTION = string.Empty;
	//private static string SQL_CONNECTION = "Data Source=mqtt_data.db";

	private static double WINDOW_SIZE = 7;
	private static double SMOOTHING = 2;
	private static double SLOPE_FACTOR = 2;
	private static double ADD_NUMBER = 0.5;

	public static double DISTANCE = 200;
	public static bool EQUAL_OPTION = false;
	public static bool USE_INFLUX = false;
	public static int USE_SRTM_METHOD = 0;
	public static double RADIUS = 10000;
	public static int COUNT_OF_SUBS = 100;
	public static double COEF_HEIGHT = 2.0;
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
			CONNECTION_METHOD = Convert.ToInt32(data["Settings"]["CONNECTION_METHOD"]);
			API_URL_CONNECTION = data["Settings"]["API_URL_CONNECTION"];
			API_LOGIN_CONNECTION = data["Settings"]["API_LOGIN_CONNECTION"];
			API_PASSWORD_CONNECTION = data["Settings"]["API_PASSWORD_CONNECTION"];

			WINDOW_SIZE = Convert.ToDouble(data["MovingAverage"]["WINDOW_SIZE"]);
			SMOOTHING = Convert.ToDouble(data["MovingAverage"]["SMOOTHING"]);
			SLOPE_FACTOR = Convert.ToDouble(data["MovingAverage"]["SLOPE_FACTOR"]);
			ADD_NUMBER = Convert.ToDouble(data["MovingAverage"]["ADD_NUMBER"]);

			DISTANCE = Convert.ToDouble(data["ElevationAnalyzer"]["DISTANCE"]);
			EQUAL_OPTION = bool.Parse(data["ElevationAnalyzer"]["EQUAL_OPTION"]);
			USE_INFLUX = bool.Parse(data["ElevationAnalyzer"]["USE_INFLUX"]);
			USE_SRTM_METHOD = Convert.ToInt32(data["ElevationAnalyzer"]["USE_SRTM_METHOD"]);
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
			data["Settings"]["CONNECTION_METHOD"] = CONNECTION_METHOD.ToString();
			data["Settings"]["API_URL_CONNECTION"] = API_URL_CONNECTION.ToString();
			data["Settings"]["API_LOGIN_CONNECTION"] = API_LOGIN_CONNECTION.ToString();
			data["Settings"]["API_PASSWORD_CONNECTION"] = API_PASSWORD_CONNECTION.ToString();

			data.Sections.AddSection("MovingAverage");
			data["MovingAverage"]["WINDOW_SIZE"] = WINDOW_SIZE.ToString();
			data["MovingAverage"]["SMOOTHING"] = SMOOTHING.ToString();
			data["MovingAverage"]["SLOPE_FACTOR"] = SLOPE_FACTOR.ToString();
			data["MovingAverage"]["ADD_NUMBER"] = ADD_NUMBER.ToString();

			data.Sections.AddSection("ElevationAnalyzer");
			data["ElevationAnalyzer"]["DISTANCE"] = DISTANCE.ToString();
			data["ElevationAnalyzer"]["EQUAL_OPTION"] = EQUAL_OPTION.ToString();
			data["ElevationAnalyzer"]["USE_INFLUX"] = USE_INFLUX.ToString();
			data["ElevationAnalyzer"]["USE_SRTM_METHOD"] = USE_SRTM_METHOD.ToString();
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
							$"CONNECTION_METHOD = [{CONNECTION_METHOD}]\n" +
							$"API_URL_CONNECTION = [{API_URL_CONNECTION}]\n" +
							$"API_LOGIN_CONNECTION = [{API_LOGIN_CONNECTION}]\n" +
							$"API_PASSWORD_CONNECTION = [{API_PASSWORD_CONNECTION}]\n" +
							
							$"WINDOW_SIZE = [{WINDOW_SIZE}]\n" +
							$"SMOOTHING = [{SMOOTHING}]\n" +
							$"SLOPE_FACTOR = [{SLOPE_FACTOR}]\n" +
							$"ADD_NUMBER = [{ADD_NUMBER}]\n" +

							$"DISTANCE = [{DISTANCE}]\n" +
							$"EQUAL_OPTION = [{EQUAL_OPTION}]\n" +
							$"USE_INFLUX = [{USE_INFLUX}]\n" +
							$"USE_SRTM_METHOD = [{USE_SRTM_METHOD}]\n" +
							$"RADIUS = [{RADIUS}]\n" +
							$"COUNT_OF_SUBS = [{COUNT_OF_SUBS}]\n" +
							$"COEF_HEIGHT = [{COEF_HEIGHT}]\n" +
							$"TILES_FOLDER = [{TILES_FOLDER}]\n" +
							$"GDAL_PYTHON = [{GDAL_PYTHON}]\n" +
							$"GDAL_PYSCRIPT = [{GDAL_PYSCRIPT}]";
	}

	private static int _topicID = 0;
	public static int TopicID { get { return _topicID; } }
	private IDataProvider dataProvider;
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
		program.dataProvider = Program.CONNECTION_METHOD switch
		{
			0 => new DbToolsDataProvider(dBTools),
			1 => new APIDataProvider(),
			_ => throw new NotImplementedException()
		};
		IQueryRepository queryRepository = new QueryRepository(program.dataProvider);

		// Глобальный словарь для хранения времени последнего изменения данных для каждого топика
		Dictionary<int, long?> lastDataChange = new Dictionary<int, long?>();

		logger.Info($"All done!");
		await Task.Delay(3000);

		while (true)
		{
			object?[,] topics = await queryRepository.GetTopicsAsync();
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
					object? latestDataTime = await queryRepository.LastedDataTimeAsync(_topicID);

					// Если есть новые данные, или это первый расчет для топика
					if (!lastDataChange.TryGetValue(_topicID, out long? value) || latestDataTime == null || ((long?)latestDataTime > value))
					{
						lastDataChange[_topicID] = (long?)latestDataTime;
						double prediction3 = await program.checkTopicConditions(_topicID);

						// Если данные прошли проверку по параметрам затопления, то топику угрожает затопление. Рассчет области затопления.
						if (prediction3 != -1)
						{
							logger.Info($"Conditions met for topic {_topicID}. Calculating area points.");

							// Вычисялем точки
							IElevationAnalyzer areaFinder = Program.USE_SRTM_METHOD switch
							{
								0 => new PythonPointAnalyzer(),
								1 => new PythonAreaAnalyzer(),
								_ => throw new NotImplementedException()
							};
							PointsPack pointsPack = areaFinder.FindArea(new Coordinate(latitude, longitude), prediction3);
							
							// Если по каким то причинам не получилось вставить данные
							if (await program.insertAreaData(pointsPack, _topicID, true, true) == -1)
							{
								logger.Warn($"Topic {_topicID} does not exist in the database or was deleted. Skipping operations.");
								continue;
							}
						}
						else
						{
							// Если данные не прошли проверку по параметрам затопления, то топику не угрожает затопление. Очистка данных области затопления. Обновляем CheckTime_Topic, чтобы отметить, что топик был проверен.
							logger.Info($"Conditions not met for topic {_topicID}. Clearing data from AreaPoints.");
							await queryRepository.DeleteTopicAreaAsync(_topicID);
							await queryRepository.UpdateTopicCheckTimeAsync(_topicID);
							logger.Info($"Data for topic {_topicID} cleared from AreaPoints and CheckTime_Topic updated.");
						}
					}
					else
					{
						// Если новых данных нет, то расчет не требуется, но обновляем CheckTime_Topic, чтобы отметить, что топик был проверен.
						logger.Info($"No new data for topic {_topicID} since last calculation. Updating CheckTime_Topic.");
						await queryRepository.UpdateTopicCheckTimeAsync(_topicID);
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

	private async Task<double> checkTopicConditions(int _topicID)
	{
		double result = -1;

		IQueryRepository queryRepository = new QueryRepository(dataProvider);
		object? altitudeObj = await queryRepository.GetAltitudeAsync(_topicID);

		if (altitudeObj != null)
		{
			double altitude = (double)altitudeObj;
			logger.Info($"Altitude for topic {_topicID}: {altitude}");

			object?[,] dataObj = await queryRepository.GetTopicDataAsync(_topicID);
			List<DataUnit> data = Enumerable.Range(0, dataObj.GetLength(0))
				.Select(i => new DataUnit(
					Convert.ToDouble(dataObj[i, 0], CultureInfo.InvariantCulture), // Value_Data
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

					double metric = new MetricMAE().Calculate(data, predictedEvents);
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

	public async Task<int> insertAreaData(PointsPack pointsPack, int _topicID, bool deleteOld = true, bool updateTime = true)
	{
		int result = -1;

		IQueryRepository queryRepository = new QueryRepository(dataProvider);

		int countTopic = await queryRepository.GetCountRowsAsync(_topicID);
		// Если запись не удалена на момент вставки
		if (countTopic > 0)
		{
			try
			{
				if (deleteOld)
				{
					await queryRepository.DeleteTopicAreaAsync(_topicID);
					await queryRepository.InsertTopicAreaAsync(_topicID, pointsPack);
				}
				else
				{
					await queryRepository.UpdateTopicAreaAsync(_topicID, pointsPack);
				}

				if (updateTime)
				{
					await queryRepository.UpdateTopicCheckTimeAsync(_topicID);
				}
				logger.Info($"Data for topic {_topicID} inserted into AreaPoints and CheckTime_Topic: updated = {updateTime}.");

				result = 1;
			}
			catch (Exception ex)
			{
				result = -1;
				logger.Info($"Insert Error! Details: {ex.Message}\n{ex.StackTrace}");
			}
		}
		else
		{
			// Если запись удалена на момент вставки
			result = -1;
		}
		
		return result;
	}
}
