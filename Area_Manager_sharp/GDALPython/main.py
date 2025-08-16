import os
from osgeo import gdal, osr
from lazy import lazy
from os import listdir
from os.path import isfile, join, getsize
import json
from rtree import index
import sys
from datetime import datetime

# Функция для записи логов в файл
def write_log(message):
    log_file = '/home/Naillin/Progs/MQTT_progs/Area_Manager-sharp/GDALPython/gdal_interface.log'  # Укажите путь к файлу логов
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")  # Формат: Год-Месяц-День Час:Минута:Секунда
    log_message = f"[{timestamp}] {message}"  # Добавляем время и дату к сообщению
    with open(log_file, 'a') as f:
        f.write(log_message + '\n')

class GDALInterface(object):
	SEA_LEVEL = 0
	def __init__(self, tif_path):
		super(GDALInterface, self).__init__()
		self.tif_path = tif_path
		self.loadMetadata()

	def get_corner_coords(self):
		ulx, xres, xskew, uly, yskew, yres = self.geo_transform
		lrx = ulx + (self.src.RasterXSize * xres)
		lry = uly + (self.src.RasterYSize * yres)
		return {
			'TOP_LEFT': (ulx, uly),
			'TOP_RIGHT': (lrx, uly),
			'BOTTOM_LEFT': (ulx, lry),
			'BOTTOM_RIGHT': (lrx, lry),
		}

	def loadMetadata(self):
		# open the raster and its spatial reference
		self.src = gdal.Open(self.tif_path)

		if self.src is None:
			write_log(f'Could not load GDAL file "{self.tif_path}"')
			raise Exception(f'Could not load GDAL file "{self.tif_path}"')
		# spatial_reference_raster = osr.SpatialReference(self.src.GetProjection())
		spatial_reference_raster = osr.SpatialReference()
		spatial_reference_raster.ImportFromEPSG(4326)

		# get the WGS84 spatial reference
		spatial_reference = osr.SpatialReference()
		spatial_reference.ImportFromEPSG(4326)  # WGS84

		# coordinate transformation
		self.coordinate_transform = osr.CoordinateTransformation(spatial_reference, spatial_reference_raster)
		gt = self.geo_transform = self.src.GetGeoTransform()
		dev = (gt[1] * gt[5] - gt[2] * gt[4])
		self.geo_transform_inv = (gt[0], gt[5] / dev, -gt[2] / dev,
								  gt[3], -gt[4] / dev, gt[1] / dev)

	@lazy
	def points_array(self):
		b = self.src.GetRasterBand(1)
		return b.ReadAsArray()

	def lookup(self, lat, lon, transform=False):
		try:
			if (transform):
				# get coordinate of the raster
				xgeo, ygeo, zgeo = self.coordinate_transform.TransformPoint(lon, lat, 0)

				# convert it to pixel/line on band
				u = xgeo - self.geo_transform_inv[0]
				v = ygeo - self.geo_transform_inv[3]
				xpix = int(self.geo_transform_inv[1] * u + self.geo_transform_inv[2] * v)
				ylin = int(self.geo_transform_inv[4] * u + self.geo_transform_inv[5] * v)

				# look the value up
				v = self.points_array[ylin, xpix]

				return v if v != -32768 else self.SEA_LEVEL
			else:
				# Преобразуем координаты (широта, долгота) в пиксельные координаты
				# Используем гео-трансформацию растра
				xgeo = lon
				ygeo = lat

				# Преобразуем географические координаты в пиксельные
				u = xgeo - self.geo_transform_inv[0]
				v = ygeo - self.geo_transform_inv[3]
				xpix = int(self.geo_transform_inv[1] * u + self.geo_transform_inv[2] * v)
				ylin = int(self.geo_transform_inv[4] * u + self.geo_transform_inv[5] * v)

				# Получаем значение высоты из растра
				v = self.points_array[ylin, xpix]

				# Возвращаем значение высоты или уровень моря, если значение равно -32768
				return v if v != -32768 else self.SEA_LEVEL
		except Exception as e:
			write_log(f'Error in lookup: {e}')
			return self.SEA_LEVEL

	def close(self):
		self.src = None

	def __enter__(self):
		return self

	def __exit__(self, type, value, traceback):
		self.close()

class GDALTileInterface(object):
	def __init__(self, tiles_folder, summary_file, open_interfaces_size=5):
		super(GDALTileInterface, self).__init__()
		self.tiles_folder = tiles_folder
		self.summary_file = summary_file
		self.index = index.Index()
		self.cached_open_interfaces = []
		self.cached_open_interfaces_dict = {}
		self.open_interfaces_size = open_interfaces_size

	def _open_gdal_interface(self, path):
		if path in self.cached_open_interfaces_dict:
			interface = self.cached_open_interfaces_dict[path]
			self.cached_open_interfaces.remove(path)
			self.cached_open_interfaces += [path]
			return interface
		else:
			interface = GDALInterface(path)
			self.cached_open_interfaces += [path]
			self.cached_open_interfaces_dict[path] = interface

			if len(self.cached_open_interfaces) > self.open_interfaces_size:
				last_interface_path = self.cached_open_interfaces.pop(0)
				last_interface = self.cached_open_interfaces_dict[last_interface_path]
				last_interface.close()
				self.cached_open_interfaces_dict[last_interface_path] = None
				del self.cached_open_interfaces_dict[last_interface_path]

			return interface

	def _all_files(self):
		return [f for f in listdir(self.tiles_folder) if isfile(join(self.tiles_folder, f)) and f.endswith(u'.tif')]

	def has_summary_json(self):
		return os.path.exists(self.summary_file)

	def create_summary_json(self):
		all_coords = []
		for file in self._all_files():
			full_path = join(self.tiles_folder, file)
			write_log(f'Processing {full_path} ... ({getsize(full_path) / 2**20} MB)')
			i = self._open_gdal_interface(full_path)
			coords = i.get_corner_coords()

			lmin, lmax = coords['BOTTOM_RIGHT'][1], coords['TOP_RIGHT'][1]
			lngmin, lngmax = coords['TOP_LEFT'][0], coords['TOP_RIGHT'][0]
			all_coords += [
				{
					'file': full_path,
					'coords': (lmin, lmax, lngmin, lngmax)
				}
			]
			write_log(f'\tDone! LAT ({lmin},{lmax}) | LNG ({lngmin},{lngmax})')

		with open(self.summary_file, 'w') as f:
			json.dump(all_coords, f)

		self.all_coords = all_coords
		self._build_index()

	def read_summary_json(self):
		with open(self.summary_file) as f:
			self.all_coords = json.load(f)
		self._build_index()

	def lookup(self, lat, lng):
		nearest = list(self.index.nearest((lat, lng), 1, objects=True))

		if not nearest:
			write_log(f'Invalid latitude/longitude: ({lat}, {lng})')
			raise Exception(f'Invalid latitude/longitude: ({lat}, {lng})')
		else:
			coords = nearest[0].object
			gdal_interface = self._open_gdal_interface(coords['file'])
			return int(gdal_interface.lookup(lat, lng, False))

	def _build_index(self):
		write_log('Building spatial index ...')
		index_id = 1
		for e in self.all_coords:
			e['index_id'] = index_id
			left, bottom, right, top = (e['coords'][0], e['coords'][2], e['coords'][1], e['coords'][3])
			self.index.insert(index_id, (left, bottom, right, top), obj=e)

	def close_all_interfaces(self):
		for interface in self.cached_open_interfaces_dict.values():
			if interface is not None:
				interface.close()
		self.cached_open_interfaces.clear()
		self.cached_open_interfaces_dict.clear()
		write_log("All GDAL interfaces closed.")

import os
import sys

# Пути к FIFO-файлам
fifo_to_python = "/home/Naillin/Progs/MQTT_progs/Area_Manager-sharp/GDALPython/tmp/csharp_to_python"
fifo_from_python = "/home/Naillin/Progs/MQTT_progs/Area_Manager-sharp/GDALPython/tmp/python_to_csharp"

# Создание FIFO, если они не существуют
if not os.path.exists(fifo_to_python):
	os.mkfifo(fifo_to_python)
if not os.path.exists(fifo_from_python):
	os.mkfifo(fifo_from_python)

def main():
	# Инициализация интерфейса
	tiles_folder = '/home/Naillin/Progs/MQTT_progs/Area_Manager-sharp/GDALPython/tilesFolder'
	summary_file = '/home/Naillin/Progs/MQTT_progs/Area_Manager-sharp/GDALPython/tilesFolder/summaryFile.json'
	tile_interface = GDALTileInterface(tiles_folder, summary_file)

	if not tile_interface.has_summary_json():
		tile_interface.create_summary_json()
	else:
		tile_interface.read_summary_json()

	# Открываем FIFO один раз
	with open(fifo_to_python, 'r') as fifo_in, open(fifo_from_python, 'w') as fifo_out:
		try:
			while True:
				line = fifo_in.readline().strip()
				if not line:
					continue  # ждем новые данные, не выходим

				if line == "EXIT":
					write_log("Exiting...")
					break

				try:
					lat, lon = map(float, line.split(','))
					height = tile_interface.lookup(lat, lon)
					if height is None:
						write_log(f'Height not found for coordinates: ({lat}, {lon})')
						result = "NULL"
					else:
						write_log(f'Height for coordinates: ({lat}, {lon}) = {height}')
						result = str(height)
				except Exception as e:
					write_log(f'Error processing coordinates input "{line}" - {e}')
					result = f"ERROR: {e}"

				# пишем в FIFO (и сразу flush, чтобы читатель получил данные)
				fifo_out.write(result + '\n')
				fifo_out.flush()

		finally:
			tile_interface.close_all_interfaces()
			write_log("Python process resources cleaned up.")

if __name__ == "__main__":
	main()
