using Area_Manager_sharp.ElevationAnalyzer;
using NLog;
using System.Diagnostics;
using System.Globalization;

namespace Area_Manager_sharp.GDALAnalyzerFolder
{
	internal class GDALPython
	{
		private static readonly string moduleName = "GDALPython";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private string _pythonPath = "GDALPython/venv/bin/python3";
		private string _scriptPath = "GDALPython/main.py";
		private string _fifoToPython = "GDALPython/tmp/csharp_to_python";  // FIFO для отправки данных в Python
		private string _fifoFromPython = "GDALPython/tmp/python_to_csharp";  // FIFO для получения данных из Python

		public GDALPython(string pythonPath, string scriptPath)
		{
			_pythonPath = pythonPath;
			_scriptPath = scriptPath;
		}

		private Process? pythonProcess;

		bool started = false;
		public void StartPythonProcess()
		{
			//logger.Info("Checking FIFOs.");
			//if (checkFIFO(_fifoToPython) && checkFIFO(_fifoFromPython))
			//{
			//	logger.Info("You have FIFOs!");
			//	// Запуск Python-скрипта
			//	ProcessStartInfo start = new ProcessStartInfo
			//	{
			//		FileName = _pythonPath,
			//		Arguments = _scriptPath,
			//		UseShellExecute = false,
			//		RedirectStandardInput = true,
			//		RedirectStandardOutput = true,
			//		RedirectStandardError = true,
			//		CreateNoWindow = true
			//	};

			//	logger.Info("Start Python.");
			//	pythonProcess = Process.Start(start);
			//	started = true;
			//	logger.Info("Python started.");
			//}
			//else
			//{
			//	logger.Error("FIFO named pipes do not exist or already have data.");
			//	started = false;
			//}

			// Запуск Python-скрипта
			ProcessStartInfo start = new ProcessStartInfo
			{
				FileName = _pythonPath,
				Arguments = _scriptPath,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			logger.Info("Start Python.");
			pythonProcess = Process.Start(start);
			started = true;
			logger.Info("Python started.");
		}

		public double GetElevation(Coordinate coordinate)
		{
			double result = -32768;

			logger.Info("GetElevation method from Python.");

			if (started)
			{
				try
				{
					// Отправка координат в Python через FIFO
					using (var writer = new StreamWriter(_fifoToPython))
					{
						string coordinates = $"{coordinate.Latitude.ToString(CultureInfo.InvariantCulture)},{coordinate.Longitude.ToString(CultureInfo.InvariantCulture)}";
						writer.WriteLine(coordinates);
						logger.Info("Coordinates sent to Python.");
					}

					// Чтение результата из FIFO
					using (var reader = new StreamReader(_fifoFromPython))
					{
						string? resultStr = reader.ReadLine();
						logger.Info("Elevation received from Python.");

						if (resultStr == null || resultStr == "NULL")
						{
							result = -32768;
						}
						else if (resultStr.StartsWith("ERROR:"))
						{
							result = -32768;
						}
						else
						{
							result = Convert.ToDouble(resultStr);
						}
					}
				}
				catch (Exception ex)
				{
					logger.Error($"Error in GetElevation: {ex.Message}");
				}
			}
			else
			{
				logger.Error("Python is not was started.");
			}

			return result;
		}

		public void StopPythonProcess()
		{
			if (started)
			{
				// Отправка команды EXIT в Python через FIFO
				using (var writer = new StreamWriter(_fifoToPython))
				{
					writer.WriteLine("EXIT");
				}

				pythonProcess?.WaitForExit();  // Ожидание завершения Python-процесса
				pythonProcess?.Close();
				started = false;
				logger.Info("Python stopped.");
			}
			else
			{
				logger.Error("Python is not was started.");
			}
		}

		private bool checkFIFO(string path)
		{
			bool result = false;

			if (File.Exists(path))
			{
				using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					if (fs.Length == 0)
						result = true;
					else
						result = false;
				}
			}
			else
			{
				result = false;
			}

			return result;
		}
	}
}
