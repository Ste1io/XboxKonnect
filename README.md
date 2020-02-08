# XboxKonnect

## Usage

```c
private ConsoleScanner _xboxKonnect;

private void InitializeXboxKonnect()
{
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

    Debug.WriteLine("Connection found: " + e.XboxConnection);
  };
}
```
