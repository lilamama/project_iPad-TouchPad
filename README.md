# iPad Drawing Host for Windows (Windows Ink & Apple Pencil Pro)

A high-performance Windows host application that turns an iPad with an Apple Pencil into a professional graphics tablet for Windows drawing applications (Photoshop, Krita, Clip Studio Paint, Paint 3D, OneNote).

---

## 🚀 Key Features

* **Native Windows Ink Pointer Injection**: Built on Win32 Synthetic Pointer API (`InjectSyntheticPointerInput` / `CreateSyntheticPointerDevice`).
  * **Zero Driver Requirement**: Runs immediately in user mode without needing driver signature enforcement or kernel-level drivers.
  * **Direct Windows Ink Pipeline**: Generates true `PT_PEN` messages recognized natively by modern drawing software.
* **Full Pressure Dynamics**: Maps normalized iPad pressure ($0.0 - 1.0$) directly to the 10-bit Windows Ink pressure space ($0 - 1024$).
* **Full 3D Stylus Orientation**: Supports altitude/azimuth spherical conversion into Windows `tiltX` and `tiltY` ($-90^\circ$ to $+90^\circ$).
* **Apple Pencil Pro Barrel Roll**: Direct mapping of iOS `rollAngle` to Windows `POINTER_PEN_INFO.rotation` ($0^\circ - 359^\circ$), dynamically rotating chisel/flat brushes in real time.
* **Apple Pencil Pro Squeeze**: Configurable trigger actions:
  * **Eraser Toggle**: Instantly toggles the stylus into an eraser tool via `PEN_FLAG_ERASER`.
  * **Barrel Switch**: Secondary pen button (eyedropper / right-click menu).
  * **Undo Shortcut**: Simulates `Ctrl + Z`.
* **Low Latency & High Throughput**: Asynchronous UDP server on port `8080` optimized for 120Hz - 240Hz iPad ProMotion refresh rates.
* **Display Mapping & Aspect Ratio Correction**: Preserves 4:3 iPad proportions on 16:9/16:10 monitors to prevent stretched drawings.
* **Watchdog Failsafe**: Automatic contact release if UDP connection drops mid-stroke to prevent stuck brush strokes.

---

## 📐 UDP JSON Protocol Specification

The host listens on **UDP Port 8080** for UTF-8 JSON payloads:

```json
{
  "type": "move",          // Event type: "hover", "down", "move", "up", "cancel"
  "x": 0.4523,             // Normalized [0.0 - 1.0] or absolute screen pixels
  "y": 0.3812,             // Normalized [0.0 - 1.0] or absolute screen pixels
  "pressure": 0.65,        // Normalized force [0.0 - 1.0]
  "tilt_x": 12.0,          // Optional: degrees [-90 to 90]
  "tilt_y": -8.0,          // Optional: degrees [-90 to 90]
  "altitude": 1.15,        // Optional: radians [0 to pi/2] (converted automatically)
  "azimuth": 2.45,         // Optional: radians [0 to 2*pi] (converted automatically)
  "roll": 45.0,            // Apple Pencil Pro: degrees [0.0 - 360.0]
  "squeeze": false,        // Apple Pencil Pro: boolean squeeze trigger
  "barrel": false,         // Stylus barrel button state
  "eraser": false,         // Stylus eraser tip active
  "timestamp": 1718000000  // Optional timestamp
}
```

---

## 🛠️ Building and Running

### Option 1: C# Host (.NET 8 / 9) - Recommended

```bash
# Navigate to the C# project
cd src/iPadDrawingHost

# Run directly
dotnet run

# Or publish as a single-file standalone executable
dotnet publish -c Release -r win-x64 --self-contained /p:PublishSingleFile=true
```

#### Command-Line Options:
* `--port 8080`: Set custom UDP port.
* `--screen virtual`: Map to all monitors across the virtual desktop (default is primary monitor).
* `--no-aspect`: Disable 4:3 aspect ratio letterboxing and stretch to full screen.
* `--squeeze undo`: Map Apple Pencil Pro squeeze to Undo (`Ctrl+Z`).
* `--squeeze barrel`: Map squeeze to barrel button (right click).
* `--squeeze hold`: Hold squeeze to erase (default is toggle).

---

### Option 2: Native C++ Host (Zero Dependencies)

The C++ host uses pure Win32 API (`ws2_32.lib`, `user32.lib`) with no external runtime requirements.

#### Compile with MSVC (Visual Studio Developer Command Prompt):
```cmd
cd cpp
cl /EHsc /O2 /std:c++20 iPadDrawingHost.cpp /link user32.lib ws2_32.lib /out:iPadDrawingHost.exe
iPadDrawingHost.exe
```

#### Compile with MinGW / GCC:
```cmd
cd cpp
g++ -O3 -std=c++20 iPadDrawingHost.cpp -lws2_32 -luser32 -o iPadDrawingHost.exe
iPadDrawingHost.exe
```

---

## 🧪 Testing with the Python Test Client

Before launching the iPad app, you can test pen pressure, tilt, barrel roll, and squeeze right from Windows:

```bash
# Run the test client
python test/test_client.py
```

* Choose **1** to run an automated S-curve stroke demonstrating smooth pressure ramp ($0.1 \to 1.0 \to 0.1$), dynamic tilt, and $360^\circ$ barrel roll.
* Choose **2** to test cursor hover movement.
* Choose **3** to test Apple Pencil Pro squeeze trigger.
* Choose **4** for a continuous 20-second drawing loop to calibrate brushes in Photoshop or Krita.

---

## 🎨 Drawing App Setup

### 1. Adobe Photoshop
* By default, modern Photoshop uses **Windows Ink**.
* If you previously configured a `PSUserConfig.txt` file forcing WinTab, delete that file or set `UseSystemStylus 1` in:
  `%APPDATA%\Adobe\Adobe Photoshop [Version]\Adobe Photoshop [Version] Settings\PSUserConfig.txt`
* In Photoshop brush settings, enable **Shape Dynamics** and set *Size Jitter Control* to **Pen Pressure**, and *Angle Jitter Control* to **Initial Direction** or **Rotation** (for Apple Pencil Pro roll).

### 2. Krita
* Go to **Settings** $\to$ **Configure Krita...** $\to$ **Tablet Settings**.
* Under **Tablet Input API**, select **Windows 8+ Pointer Input (Windows Ink)** (do not use WinTab).
* Restart Krita. Dynamic pressure, tilt, and barrel roll will work out of the box.

### 3. Clip Studio Paint
* Go to **File** $\to$ **Preferences** $\to$ **Tablet**.
* Under *Using tablet service*, select **Tablet PC** (Windows Ink).
