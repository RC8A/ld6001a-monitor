# LD6001A Monitor

**English** | [Русский](README.ru.md)

![LD6001A Monitor](Source/Assets/ld-monitor-logo.png)

An independent Windows application for testing the **Hi-Link HLK-LD6001A-60G** presence and multi-target tracking radar over USB–UART.

**Platform:** Windows 10 x64 · WPF · .NET Framework 4.7.2 or later  
**Author:** Dmitry Popov · Developed with assistance from OpenAI Codex  
**License:** PolyForm Noncommercial 1.0.0

This project is not affiliated with Hi-Link and does not include the manufacturer's software or firmware.

## Features

- **DEBUG 0:** people count. **DEBUG 3:** target IDs, coordinates and velocities.
- Radar view with fading target trails, adjustable persistence, and X/Y mirroring.
- Draw and resize the hardware detection zone with the mouse, or edit its boundaries in the table.
- Read sensor settings on connection without automatically overwriting them.
- Apply edits one second after the last change, then verify the ACK and read parameters back.
- Highlight confirmed DEBUG and START/STOP states. An unknown state is not presented as stopped.
- Wait up to 30 seconds for the sensor to initialize, retrying parameter reads.
- Edit all 11 supported parameters in a table; time values are displayed in seconds.
- Save the COM port, baud rate and display preferences in `data/settings.ini`.
- Record raw UART bytes, command/response logs and target CSV data for each session.
- **English and Russian interfaces**, selectable through **Language / Язык**. The selection is saved.

## Getting started

Download a ZIP from [Releases](https://github.com/RC8A/ld6001a-monitor/releases), extract it into a writable folder, and run `LD6001A Monitor.exe`. No installation is required.

The default connection is **COM7, 115200 baud, 8N1, no flow control**. Select your USB–UART port and click **Connect**. The first command is `AT+READ`; connecting does not reapply locally saved sensor parameters.

Use **Demo** to try the interface without hardware. Select **DEBUG 3** for the radar view. DEBUG 0 does not transmit coordinates and may report periodically according to its heartbeat interval.

### Wiring

Connect the sensor's TX to the adapter's RX, RX to TX, and share GND. The bare module uses 3.3 V power and logic; verify the requirements of your specific board. Do not assume the adapter's 3.3 V output can supply the radar's required current.

## Building and testing

From the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Source\Build.ps1
```

The build uses the .NET Framework compiler included with Windows. No NuGet packages or third-party runtime libraries are required. The script closes the previous instance of this application, builds the EXE, runs checks and opens the new build. Use `-NoLaunch` for CI.

Hardware-free checks cover packet parsing, ACK/readback verification, local settings, zone geometry, mirroring, timers, numeric controls and interface layout. Rendered WPF images are saved to `Source/Previews` for visual review. These checks do not open a COM port and do not establish real-world detection quality.

## Versions and releases

`version.txt` contains the release version in `major.minor.patch` format. The current release is **1.0.0**. Successful local builds increment a separate build number, shown in the EXE metadata and **Help → About**.

To prepare the next patch release:

```powershell
powershell.exe -NoProfile -File .\scripts\Bump-Version.ps1 -Part Patch
```

This changes `1.0.0` to `1.0.1`. After building and testing, create the release package:

```powershell
powershell.exe -NoProfile -File .\scripts\Package-Release.ps1
```

The ZIP, SHA-256 checksum and staging folder are created in `dist`. Release packages exclude personal settings, logs, old builds and the manufacturer's programs.

## Known limitations

The documented protocol exposes **one set of X/Y hardware boundaries**, additionally limited by the detection radius. Support for multiple independent hardware zones has not been confirmed.

The zone must include the sensor origin. Negative boundaries range from −500 to −20 cm; positive boundaries range from 20 to 500 cm. The radar view maps 100 cm to one coordinate unit; verify this against known distances with your hardware.

Metal structures and multipath reflections can affect detection. Configuration changes cannot eliminate hardware limitations. This application does not update the sensor firmware.

## Contributing

Please submit changes through pull requests. The recommended workflow is **CI checks → maintainer review → squash merge**. See [CONTRIBUTING.md](CONTRIBUTING.md) for the contribution policy and [PUBLISHING.md](PUBLISHING.md) for release preparation instructions; these documents are currently in Russian.

## License and attribution

**PolyForm Noncommercial License 1.0.0** — see [LICENSE.txt](LICENSE.txt) and [COPYRIGHT](COPYRIGHT). Noncommercial use, modification and distribution are permitted under the license terms. Commercial use requires separate permission from the rights holder. This is a source-available project with a noncommercial license, not OSI-approved open source.

Hi-Link programs and documentation are not bundled with this repository or its releases. Protocol reference: [manufacturer's V1.2 manual](https://r0.hlktech.com/download/HLK-LD6001A-60G/1/HLK-LD6001A-60G%E4%BA%BA%E4%BD%93%E8%BD%A8%E8%BF%B9%E9%9B%B7%E8%BE%BE%E6%A8%A1%E7%BB%84%E8%AF%B4%E6%98%8E%E4%B9%A6V1.2.pdf).
