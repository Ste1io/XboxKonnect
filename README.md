# XboxKonnect

## Usage

```c
_xboxKonnect = new ConsoleScanner
{
  ScanFrequency = new TimeSpan(0, 0, 1),
  DisconnectTimeout = new TimeSpan(0, 0, 3),
  RemoveOnDisconnect = true,
}.StartScanning();

_xboxKonnect.AddConnectionEvent += delegate (object sender, OnAddConnectionEventArgs e)
{
  var con = _xboxManager.OpenConsole(e.XboxConnection.IP.Address.ToString());

  try
  {
    e.XboxConnection.CPUKey = CPUKey.Parse(con.GetCPUKey());
  }
  catch (Exception ex)
  {
    Trace.WriteLine(ex);
  }

  Invoke((MethodInvoker)(() => AddConnectionButton(e.XboxConnection)));
};
```
