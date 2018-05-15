using System;
using System.IO.Ports;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BNO055.Models;

namespace BNO055
{

	public class BNODevice
	{
		private SerialPort port;
		
		readonly int retryMax;
		private uint packets;
		private uint timeouts;
		private byte[] i2cBuffer = new byte[512];
		private BNOValues values { get; set; } = new BNOValues();

		/// <summary>
		/// Device inititalization status
		/// </summary>
		public Boolean initialized { get; private set; } = false;

		/// <summary>
		/// Current quaterion position
		/// </summary>
		public Quaternion position
		{
			get
			{
				return new Quaternion(values.quartX, values.quartY, values.quartZ, values.quartW);
			}
		}
		/// <summary>
		/// Current calibration statuses
		/// </summary>
		public Models.Calibration calibration
		{
			get
			{
				return values.calibration;
			}
		}

		/// <summary>
		/// Current connection health: 100 - packet loss (percentage time outs/requests)
		/// 100 indicates no dropped requests
		/// </summary>
		public float connectionHealth
		{
			get
			{
				return 100 - (((float)timeouts * 100) / (float)packets);
			}
		}

		/// <summary>
		/// Creates a BNO055 software sensor, you must then asynchronously call BNO055.Bootstrap to initialize the hardware device
		/// </summary>
		/// <param name="retries">Maximum number of times to retry command after a timeout (5)</param>
		public BNODevice(int retries = 5)
		{
			retryMax = retries;
			initialized = false;
		}
		/// <summary>
		/// Boostrap and configure the BNO sensor device
		/// </summary>
		/// <param name="COM">COM port string identifier (eg. COM0)</param>
		/// <returns></returns>
		public async Task Bootstrap(String COM)
		{

			initialized = false;
			port = new SerialPort(COM, 115200, Parity.None, 8, StopBits.One);
			port.ReadTimeout = 1000;
			port.WriteTimeout = 1000;
			port.Open();
			await Task.Delay(300);

			// Set to config mode
			await _SetMode(Registers.opmode.OPERATION_MODE_CONFIG);
			await Task.Delay(300);

			// Set to normal power mode
			await _SetBytes(Registers.reg.BNO055_PWR_MODE_ADDR, new byte[1] {
				(byte) Registers.powermode.POWER_MODE_NORMAL
			});
			await Task.Delay(300);

			// Set config page
			await _SetBytes(Registers.reg.BNO055_PAGE_ID_ADDR, new byte[1] { 0 });
			await Task.Delay(300);

			// Set units
			byte unitselect = (1 << 7) | // Orientation = Android
				(0 << 4) | // Temperature = Celsius
				(0 << 2) | // Euler = Degrees
				(1 << 1) | // Gyro = R
				(0 << 0); // Accelerometer = m/s^2

			await _SetBytes(Registers.reg.BNO055_UNIT_SEL_ADDR, new byte[] { unitselect });
			await Task.Delay(300);

			//var correction = Quaternion.Euler(-q.eulerAngles.x, q.eulerAngles.z, q.eulerAngles.y);
			byte axisMap = (byte)Convert.ToInt32("00010010", 2);
			//setBytes(Registers.reg.BNO055_AXIS_MAP_CONFIG_ADDR, new byte[] { axisMap });
			await Task.Delay(300);

			byte axisSign = (byte)Convert.ToInt32("0000100", 2);
			//setBytes(Registers.reg.BNO055_AXIS_MAP_SIGN_ADDR, new byte[] { axisSign });
			await Task.Delay(300);

			// Send self test
			var selftest = await _SelfTest();
			await Task.Delay(300);

			if (selftest != 0x0f)
			{
				throw new Exception("Self test failed: " + (int)selftest);
			}

			// Read system error
			var syserror = await GetSystemError();
			await Task.Delay(300);
			if (syserror != 0x00)
			{
				throw new Exception("System error: " + (int)syserror);
			}

			// Set running mode
			await _SetMode(Registers.opmode.OPERATION_MODE_NDOF);
			await Task.Delay(300);

			port.ReadTimeout = 30;
			port.WriteTimeout = 30;

			initialized = true;
		}
		private void _CheckInitialized()
		{
			if (initialized) { return; }
			throw new Exception("Device has not been initialized, call bootstrap method to initialize.");
		}
		private async Task _SetMode(Registers.opmode mode)
		{
			await _SetBytes(Registers.reg.BNO055_OPR_MODE_ADDR, new byte[1] {
				(byte) mode
			});

		}
		/// <summary>
		/// Get current operating mode
		/// </summary>
		/// <returns></returns>
		public async Task<Registers.opmode> GetMode()
		{
			_CheckInitialized();
			return await _GetMode();
		}
		private async Task<Registers.opmode> _GetMode()
		{
			await _GetBytes(Registers.reg.BNO055_OPR_MODE_ADDR, 1);
			return (Registers.opmode) i2cBuffer[2];
		}

		/// <summary>
		/// Send a device reset
		/// </summary>
		/// <returns></returns>
		public async Task Reset()
		{
			_CheckInitialized();
			await _Reset();
		}
		private async Task _Reset()
		{
			await _SetBytes(Registers.reg.BNO055_SYS_TRIGGER_ADDR, new byte[1] { 0x20 }, noAck: true);
		}

		/// <summary>
		/// Perform a self test
		/// </summary>
		/// <returns>
		/// Self Test Results (0x0F = all pass)
		/// --------------------------------
		/// Bit 0 = Accelerometer self test
		/// Bit 1 = Magnetometer self test
		/// Bit 2 = Gyroscope self test
		/// Bit 3 = MCU self test
		/// </returns>
		public async Task<byte> SelfTest()
		{
			_CheckInitialized();
			return await _SelfTest();
		}
		private async Task<byte> _SelfTest()
		{
			await _SetBytes(Registers.reg.BNO055_SYS_TRIGGER_ADDR, new byte[1] { 0x1 });
			await _GetBytes(Registers.reg.BNO055_SELFTEST_RESULT_ADDR, 1);
			return i2cBuffer[2];
		}
		/// <summary>
		/// Get current package temperature
		/// </summary>
		/// <returns></returns>
		public async Task<int> GetTemperature()
		{
			await _GetBytes(Registers.reg.BNO055_TEMP_ADDR, 1);
			return (int)i2cBuffer[2];
		}

		/// <summary>
		/// Get current system error value
		/// </summary>
		/// <returns></returns>
		public async Task<Mappings.bnoError> GetSystemError()
		{
			await _GetBytes(Registers.reg.BNO055_SYS_ERR_ADDR, 1);
			return (Mappings.bnoError)i2cBuffer[2];
		}

		/// <summary>
		/// Request register values
		/// </summary>
		/// <returns>bool: values successfully requested</returns>
		public async Task UpdatePosition()
		{

			//getBytes(0, (uint)bno055.reg.MAG_RADIUS_MSB_ADDR);
			//var escale = 1.00/16.00;
			//valuesCollection.eulerX = (float) escale * ((Int16) (i2cBuffer[off + 0x1A] | (i2cBuffer[off + 0x1B] << 8)));
			//valuesCollection.eulerY = (float) escale * ((Int16) (i2cBuffer[off + 0x1C] | (i2cBuffer[off + 0x1D] << 8)));
			//valuesCollection.eulerZ = (float) escale * ((Int16) (i2cBuffer[off + 0x1E] | (i2cBuffer[off + 0x1F] << 8)));

			await _GetBytes(Registers.reg.BNO055_QUATERNION_DATA_W_LSB_ADDR, 8);

			var qscale = (1.00 / (1 << 14));
			var off = 2;
			values.quartW = (float)qscale * ((Int16)(i2cBuffer[off + 0] | (i2cBuffer[off + 1] << 8)));
			values.quartX = (float)qscale * ((Int16)(i2cBuffer[off + 2] | (i2cBuffer[off + 3] << 8)));
			values.quartY = (float)qscale * ((Int16)(i2cBuffer[off + 4] | (i2cBuffer[off + 5] << 8)));
			values.quartZ = (float)qscale * ((Int16)(i2cBuffer[off + 6] | (i2cBuffer[off + 7] << 8)));
		}
		/// <summary>
		/// Request calibration status values
		/// </summary>
		public async Task UpdateCalibrationStatus()
		{

			await _GetBytes(Registers.reg.BNO055_CALIB_STAT_ADDR, 1);

			values.calibration.calibration_raw = i2cBuffer[2];
			values.calibration.calibration_magnetometer = (byte)(i2cBuffer[2] & (((1 << 2) - 1) << 0));
			values.calibration.calibration_accelerometer = (byte)((i2cBuffer[2] & (((1 << 2) - 1) << 2)) >> 2);
			values.calibration.calibration_gyroscope = (byte)((i2cBuffer[2] & (((1 << 2) - 1) << 4)) >> 4);
			values.calibration.calibration_system = (byte)((i2cBuffer[2] & (((1 << 2) - 1) << 6)) >> 6);
		}

		private async Task _GetBytes(Registers.reg registerAddress, uint length)
		{
			if ((packets / 1000) > 0 || (timeouts / 1000) > 0)
			{
				packets = packets / 4;
				timeouts = timeouts / 4;
			}
			packets++;

			var l = 4;
			var payload = new byte[l];
			payload[0] = 0xAA; // Start Byte
			payload[1] = 0x01; // Read
			payload[2] = (byte)((byte)registerAddress & 0xFF);
			payload[3] = (byte)(length & 0xFF);

			await Task.Run(() =>
			{
				bool passed = false;
				int r = 0;
				while (!passed && r <= retryMax)
				{
					try
					{
						port.DiscardInBuffer();
						port.Write(payload, 0, l);

						for (int i = 0; i < length + 2; i++)
						{
							i2cBuffer[i] = (byte)port.ReadByte();
						}
						passed = true;
					}
					catch (TimeoutException)
					{
						r++;
						timeouts++;
					}
				}

				if (!passed)
				{
					throw new Exception("Retried exceeded, request timed out");
				}

				if (i2cBuffer[0] != 0xBB)
				{
					throw new Exception("Register read error - byte[0] != 0xBB");
				}
			});
		}
		private async Task _SetBytes(Registers.reg registerAddress, byte[] values, bool noAck = false)
		{
			if ((packets / 1000) > 0 || (timeouts / 1000) > 0)
			{
				packets = packets / 4;
				timeouts = timeouts / 4;
			}
			packets++;

			var l = 4 + values.Length;
			var payload = new byte[l];
			payload[0] = 0xAA; // Start Byte
			payload[1] = 0x00; // Write
			payload[2] = (byte)((byte)registerAddress & 0xFF);
			payload[3] = (byte)values.Length;
			var i = 4;
			foreach (var dat in values)
			{
				payload[i] = (byte)(dat & 0xFF);
				i++;
			}
			await Task.Run(() =>
			{
				bool passed = false;
				int r = 0;
				while (!passed && r <= retryMax)
				{
					try
					{
						port.DiscardInBuffer();
						port.Write(payload, 0, l);

						if (noAck)
						{
							return;
						}

						for (int x = 0; x < 2; x++)
						{
							i2cBuffer[x] = (byte)port.ReadByte();
						}
						passed = true;
					}
					catch (TimeoutException)
					{
						r++;
						timeouts++;
					}
				}
				if (!passed)
				{
					throw new Exception("Retries exceeded, request timed out");
				}
				if (i2cBuffer[0] != 0xEE && i2cBuffer[1] != 0x01)
				{
					throw new Exception("Register write error - byte[0] != 0xEE && byte[1] != 0x01");
				}
			});
		}
	}
}