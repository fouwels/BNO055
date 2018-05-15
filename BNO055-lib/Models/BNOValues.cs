using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BNO055.Models {
	public class BNOValues {
		public float quartW { get; set; } = 0;
		public float quartX { get; set; } = 0;
		public float quartY { get; set; } = 0;
		public float quartZ { get; set; } = 0;
		public Calibration calibration { get; set; } = new Calibration ();
	}

	public class Calibration {
		public byte calibration_system { get; set; } = 0;
		public byte calibration_gyroscope { get; set; } = 0;
		public byte calibration_accelerometer { get; set; } = 0;
		public byte calibration_magnetometer { get; set; } = 0;
		public byte calibration_raw { get; set; } = 0;
	}
}