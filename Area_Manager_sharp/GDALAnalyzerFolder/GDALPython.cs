using Area_Manager_sharp.ElevationAnalyzer;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.PortableExecutable;

namespace Area_Manager_sharp.GDALAnalyzerFolder
{
	internal class GDALPython
	{
		private Process? pythonProcess;
		private StreamWriter? writer;
		private StreamReader? reader;
		private StreamReader? errorReader;

		public void StartPythonProcess()
		{
			string pythonPath = "/GDALPython/venv/bin/python3";
			string scriptPath = "/GDALPython/main.py";

			ProcessStartInfo start = new ProcessStartInfo
			{
				FileName = pythonPath,
				Arguments = scriptPath,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			};

			pythonProcess = Process.Start(start);
			if (pythonProcess != null)
			{
				writer = pythonProcess.StandardInput;
				reader = pythonProcess.StandardOutput;
				errorReader = pythonProcess.StandardError;
			}
		}

		public double GetElevation(Сoordinate coordinate)
		{
			double result = -32768;

			if (writer == null || reader == null || errorReader == null)
			{
				throw new InvalidOperationException("Python process is not started.");
			}

			string coordinates = $"{coordinate.Latitude.ToString(CultureInfo.InvariantCulture)},{coordinate.Longitude.ToString(CultureInfo.InvariantCulture)}";
			writer.WriteLine(coordinates);

			string? resultStr = reader.ReadLine();
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

			string errors = errorReader.ReadToEnd();
			if (!string.IsNullOrEmpty(errors))
			{
				Console.WriteLine("Errors: " + errors);
			}

			return result;
		}

		public void StopPythonProcess()
		{
			if (writer != null)
			{
				writer.WriteLine("EXIT");  // Отправляем команду для завершения работы
				writer.Close();
			}
			reader?.Close();
			errorReader?.Close();
			pythonProcess?.Close();
		}
	}
}
