Prototyping repo for OpenRA TD with remastered assets.

This repo is made available as a proof of concept demonstrating the capabilities of OpenRA using the C&C Remastered Collection assets.
Performance, memory usage, and loading times have not been optimized, and do not reflect the expected final requirements.

A dedicated GPU is recommended. Use at your own risk.

## Asset Installation

Tiberian Dawn HD loads assets directly from an existing digital installation of the C&C Remastered Collection.

#### Windows

Install the C&C Remastered Collection through Steam or Origin.

#### Linux

Install the C&C Remastered Collection through Steam using the following procedure:

1. Install Steam through your distro package manager or [Flathub](https://flathub.org/apps/details/com.valvesoftware.Steam).
2. Launch Steam and enable "Enable Steam Play for all other titles" from the Settings &rarr; Steam Play menu.
3. Install C&C Remastered Collection from the Library tab.

#### macOS

Install the C&C Remastered Collection through Steam using the following procedure:

1. Make sure that Steam is installed.
2. Create a file `~/Library/Application Support/Steam/steamapps/appmanifest_1213210.acf` with contents
   ```
   "AppState"
   {
   	"appid"		"1213210"
   	"Universe"		"1"
   	"installdir"		"CnCRemastered"
   	"StateFlags"		"1026"
   }
   ```
3. Restart steam and log into an account that owns the C&C Remastered Collection.

The game files will automatically download.
