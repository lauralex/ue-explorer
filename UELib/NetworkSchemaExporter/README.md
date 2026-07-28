# UE3 network schema exporter

This read-only command-line tool exports the version-sensitive package map and
class net-field ordering required by an external UE3 replication decoder. Its
first consumer is NebulaProxy's Rocket League ball/car telemetry path.

Rocket League packages must be decrypted with RLUPKTool before they are passed
to this exporter.

```powershell
dotnet run --project .\UELib\NetworkSchemaExporter -- `
  .\rl-network-schema.json `
  C:\path\to\Core_decrypted.upk `
  C:\path\to\Engine_decrypted.upk `
  C:\path\to\IpDrv_decrypted.upk `
  C:\path\to\ProjectX_decrypted.upk `
  C:\path\to\TAGame_decrypted.upk
```

The generated JSON records package versions, GUIDs, per-generation net-object
counts, trajectory actor/archetype objects, inheritance, and ordered local
network fields. It never writes to an input package.
