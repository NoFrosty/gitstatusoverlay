# Git Status Overlay for Unity

**Git Status Overlay** is a Unity Editor extension that displays Git status icons directly in the Project window, helping you visualize which assets are modified, added, ignored, or renamed in your repository.

## Features

- Shows overlay icons for:
  - Added (untracked) files
  - Modified files
  - Renamed/moved files
  - Ignored files
  - Files with changes available in origin (remote)
  - Files with local commits ready to push
  - Warning for potential conflicts (modified locally and in origin)
  - Folders (optional)
- Multiple icons can be displayed on the same file when applicable
- Configurable icon, size, opacity, and position
- Optional automatic fetching from remote repository
- Configurable fetch interval
- Supports Unity's package system (`Packages/com.caesiumgames.gitstatusoverlay`)
- Editor window for easy configuration

## Installation

1. **Via Unity Package Manager (UPM):**

   - Copy the `com.caesiumgames.gitstatusoverlay` folder into your project's `Packages` directory.
   - Or add the following to your `manifest.json`:
     ```json
     "com.caesiumgames.gitstatusoverlay": "https://github.com/CaesiumIndustries/gitstatusoverlay.git"
     ```

2. **Manual:**
   - Download or clone this repository.
   - Place the `com.caesiumgames.gitstatusoverlay` folder in your project's `Packages` directory.

## Usage

1. **Open the configuration window:**

   - Go to `Window > Git Status Overlay`.

2. **Configure icons and options:**

   - Assign your preferred icons for each status.
   - Adjust icon size, opacity, and position.
   - Toggle folder overlays and status types.

3. **Configure remote tracking (optional):**

   - Enable auto-fetch to periodically check for remote changes.
   - Set the fetch interval (in seconds).
   - Enable "Show Push Available" to see files with unpushed commits.
   - Enable "Detect Potential Conflicts" to warn about files modified both locally and remotely.

4. **Show the config asset in the Project window:**

   - Use the "Open Config" button in the window to locate and select the config asset.

5. **Refresh Git status:**
   - Click "Refresh Git Status" in the window to update overlays.

## Requirements

- Unity 6000.0 or newer (recommended)
- Git must be installed and available in your system PATH

## Customization

- You can use your own icons by assigning them in the config window.
- The overlay supports most common Git statuses (see `GitStatus` enum for details).
- Multiple icons will be displayed side-by-side when a file has multiple statuses.

## Troubleshooting

- If icons do not appear, ensure your config asset is created and assigned.
- Make sure Git is installed and accessible from the command line.
- Only files inside the `Assets/` folder are tracked for overlays.
- Make sure your project is using Git.
- Auto-fetch requires a valid remote repository connection.

## Icons Attribution

The following icons from Flaticon are used in this project. Colors may have been modified for better visibility and integration with Unity's interface:

- [Plus icons created by kliwir art - Flaticon](https://www.flaticon.com/free-icons/plus)
- [Modify icons created by riajulislam - Flaticon](https://www.flaticon.com/free-icons/modify)
- [Minus icons created by Alfredo Hernandez - Flaticon](https://www.flaticon.com/free-icons/minus)
- [Next icons created by riajulislam - Flaticon](https://www.flaticon.com/free-icons/next)
- [Rename icons created by afif fudin - Flaticon](https://www.flaticon.com/free-icons/rename)
- [Save file icons created by Fathema Khanom - Flaticon](https://www.flaticon.com/free-icons/save-file)
- [Publish icons created by Fathema Khanom - Flaticon](https://www.flaticon.com/free-icons/publish)
- [Alert icons created by riajulislam - Flaticon](https://www.flaticon.com/free-icons/alert)

## License

All Rights Reserved - Caesium Games

## Credits

Developed by Caesium Games.

---

**For issues or contributions, please open a pull request or issue on GitHub.**
