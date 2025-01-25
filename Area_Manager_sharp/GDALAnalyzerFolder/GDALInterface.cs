using OSGeo.GDAL;
using OSGeo.OSR;

namespace Area_Manager_sharp.GDALAnalyzerFolder
{
	public class GDALInterface : IDisposable
	{
		private Dataset _dataset; // GDAL Dataset для работы с .tif файлом
		private Band _band;       // Растровый слой (band) для чтения данных
		private CoordinateTransformation _transform; // Преобразование координат
		private double[] _geoTransform; // Гео-трансформация для перевода координат в пиксели
		private double[] _invGeoTransform = new double[0]; // Обратная гео-трансформация
		public const int SeaLevel = 0; // Значение по умолчанию для уровня моря

		public GDALInterface(string tifPath)
		{
			Gdal.AllRegister(); // Инициализация GDAL
			_dataset = Gdal.Open(tifPath, Access.GA_ReadOnly); // Открытие .tif файла

			if (_dataset == null)
				throw new Exception("Не удалось открыть файл: " + tifPath);

			_band = _dataset.GetRasterBand(1); // Получаем первый растровый слой
			_geoTransform = new double[6];
			_dataset.GetGeoTransform(_geoTransform); // Получаем гео-трансформацию

			//// Инициализация преобразования координат
			//SpatialReference srcSrs = new SpatialReference(_dataset.GetProjection()); // Пространственная система координат файла
			//SpatialReference dstSrs = new SpatialReference("");
			//dstSrs.ImportFromEPSG(4326); // WGS84 (широта/долгота)

			// Инициализация преобразования координат
			SpatialReference srcSrs = new SpatialReference("");
			srcSrs.ImportFromEPSG(4326); // Явно указываем WGS84

			SpatialReference dstSrs = new SpatialReference("");
			dstSrs.ImportFromEPSG(4326); // WGS84 (широта/долгота)
			_transform = new CoordinateTransformation(dstSrs, srcSrs);

			ComputeInverseTransform(); // Вычисляем обратную гео-трансформацию
		}

		// Возвращает гео-трансформацию
		public double[] GetGeoTransform() => _geoTransform;

		// Возвращает Dataset
		public Dataset GetDataset() => _dataset;

		// Вычисление обратной гео-трансформации
		private void ComputeInverseTransform()
		{
			double det = _geoTransform[1] * _geoTransform[5] - _geoTransform[2] * _geoTransform[4];
			_invGeoTransform = new[]
			{
			_geoTransform[0],
			_geoTransform[5] / det,
			-_geoTransform[2] / det,
			_geoTransform[3],
			-_geoTransform[4] / det,
			_geoTransform[1] / det
		};
		}

		// Получение высоты по координатам (широта, долгота)
		public double GetElevation(double lat, double lon, bool transform = false)
		{
			try
			{
				if (transform)
				{
					double[] transformPoint = new double[3];
					_transform.TransformPoint(transformPoint, lon, lat, 0); // Преобразование координат

					double x = transformPoint[0];
					double y = transformPoint[1];

					// Перевод координат в пиксели
					double u = x - _invGeoTransform[0];
					double v = y - _invGeoTransform[3];
					int xpix = (int)(_invGeoTransform[1] * u + _invGeoTransform[2] * v);
					int ylin = (int)(_invGeoTransform[4] * u + _invGeoTransform[5] * v);

					// Чтение значения высоты
					double[] buf = new double[1];
					_band.ReadRaster(xpix, ylin, 1, 1, buf, 1, 1, 0, 0);

					return buf[0] == -32768 ? SeaLevel : buf[0]; // Возвращаем высоту или уровень моря
				}
				else
				{
					// Перевод координат (широта/долгота) в пиксели
					double u = lon - _invGeoTransform[0];
					double v = lat - _invGeoTransform[3];
					int xpix = (int)(_invGeoTransform[1] * u + _invGeoTransform[2] * v);
					int ylin = (int)(_invGeoTransform[4] * u + _invGeoTransform[5] * v);

					// Чтение значения высоты
					double[] buf = new double[1];
					_band.ReadRaster(xpix, ylin, 1, 1, buf, 1, 1, 0, 0);

					return buf[0] == -32768 ? SeaLevel : buf[0]; // Возвращаем высоту или уровень моря
				}

			}
			catch
			{
				return SeaLevel; // В случае ошибки возвращаем уровень моря
			}
		}

		// Освобождение ресурсов
		public void Dispose()
		{
			_band?.Dispose();
			_dataset?.Dispose();
			_transform?.Dispose();
		}
	}
}
