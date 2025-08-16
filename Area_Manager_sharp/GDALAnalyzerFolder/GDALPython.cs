using Area_Manager_sharp.ElevationAnalyzerFolder;
using NLog;
using System.Diagnostics;
using System.Globalization;

namespace Area_Manager_sharp.GDALAnalyzerFolder
{
	internal class GDALPython : IDisposable
	{
		private static readonly string moduleName = "GDALPython";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private string _pythonPath = "GDALPython/venv/bin/python3";
		private string _scriptPath = "GDALPython/main.py";
		private string _fifoToPython = "GDALPython/tmp/csharp_to_python";  // FIFO для отправки данных в Python
		private string _fifoFromPython = "GDALPython/tmp/python_to_csharp";  // FIFO для получения данных из Python

		private Process _pythonProcess;
		private StreamWriter _writer;
		private StreamReader _reader;

		private int debugWrite = 200;
		private static int debugWriteCheck = 0;

		public GDALPython(string pythonPath, string scriptPath)
		{
			_pythonPath = pythonPath;
			_scriptPath = scriptPath;

			//// Удаляем старые FIFO если они остались
			//if (File.Exists(_fifoToPython)) File.Delete(_fifoToPython);
			//if (File.Exists(_fifoFromPython)) File.Delete(_fifoFromPython);

			//// Создаем новые
			//Mono.Unix.Native.Syscall.mkfifo(_fifoToPython, (Mono.Unix.Native.FilePermissions)0x1B6); // 0666
			//Mono.Unix.Native.Syscall.mkfifo(_fifoFromPython, (Mono.Unix.Native.FilePermissions)0x1B6);
		}

		private Process? pythonProcess;

		public void StartPythonProcess()
		{
			// Запуск Python-скрипта
			_pythonProcess = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = _pythonPath,
					Arguments = _scriptPath,
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};

			logger.Info("Start Python.");
			_pythonProcess.Start();
			// Открываем FIFO один раз
			logger.Info("Open writer.");
			_writer = new StreamWriter(_fifoToPython) { AutoFlush = true };
			logger.Info("Open reader.");
			_reader = new StreamReader(_fifoFromPython);
			logger.Info("Python started.");
		}

		public double GetElevation(Coordinate coordinate)
		{
			double result = -32768;

			//logger.Info("GetElevation method from Python.");

			try
			{
				// Отправка координат в Python через FIFO
				string coordinates = $"{coordinate.Latitude.ToString(CultureInfo.InvariantCulture)},{coordinate.Longitude.ToString(CultureInfo.InvariantCulture)}";
				_writer.WriteLine(coordinates);

				// Чтение результата из FIFO
				string? resultStr = _reader.ReadLine();
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
					if (debugWriteCheck >= debugWrite)
					{
						logger.Info($"Высота точки {coordinate.Latitude}, {coordinate.Longitude}: {result}.");
						debugWriteCheck = 0;
					}
					debugWriteCheck++;
				}
			}
			catch (Exception ex)
			{
				logger.Error($"Error in GetElevation! Точка {coordinate.Latitude}, {coordinate.Longitude}. Details: {ex.Message}\n{ex.StackTrace}");
			}

			return result;
		}

		public void Dispose()
		{
			try
			{
				_writer.WriteLine("EXIT");
				_writer.Dispose();
				_reader.Dispose();

				if (!_pythonProcess.HasExited)
					_pythonProcess.Kill();
			}
			catch { }

			logger.Info("Python stoped.");
		}
	}
}
