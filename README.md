# BNO055
C# Software driver for the Bosch Sensortec BNO055. ([datasheet](https://ae-bst.resource.bosch.com/media/_tech/media/datasheets/BST_BNO055_DS000_14.pdf))
 
Targets [.NET Standard 2.0](https://docs.microsoft.com/en-us/dotnet/standard/net-standard).

BNO055 must be set to UART mode by pulling PS1 high (10k).
 
## Installation
Nuget: [https://www.nuget.org/packages/BNO055/](https://www.nuget.org/packages/BNO055/)
 ```powershell
Install-Package BNO055 -Version 1.0.1 
 ```

## Usage

```csharp
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
	Quaternion p = bno.position;
	Calibration c = bno.calibration;

	// Read properties
	var ch = bno.connectionHealth;
	var init = bno.initialized;

}
```
## Example
```csharp
async static Task Run()
{
	var bno = new BNO055.BNODevice();
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
