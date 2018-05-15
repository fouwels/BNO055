# BNO055
 C# software driver for the Bosch Sensortec BNO055

```
async static Task Run()
{
	// Connect and initialize
	var bno = new BNO055.BNODevice();
	await bno.Bootstrap("COM9"); // COMX

	// Commands
	await bno.GetMode();
	await bno.GetSystemError();
	await bno.GetTemperature();
	await bno.Reset();
	await bno.SelfTest();

	// Update host data registers
	await bno.UpdateCalibrationStatus();
	await bno.UpdatePosition();

	// Read computed data values
	var p = bno.position;
	var c = bno.calibration;

	// Read properties
	var ch = bno.connectionHealth;
	var init = bno.initialized;

	// Example
	bno = new BNO055.BNODevice();
	await bno.Bootstrap("COM9");

	int i = 2;
	while (true)
	{
		await bno.UpdateCalibrationStatus();
		Console.Write(bno.calibration.calibration_accelerometer);
		Console.Write(bno.calibration.calibration_gyroscope);
		Console.Write(bno.calibration.calibration_magnetometer);
		Console.Write(bno.calibration.calibration_system);

		await bno.UpdatePosition();
		Console.WriteLine(bno.position);


		if (i % 100 == 1)
		{
			Console.Write(bno.connectionHealth);
			i = 2;
		}
		i++;
	}
}
```
