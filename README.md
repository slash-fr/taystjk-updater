# TaystJK Updater

**TaystJK Updater** is a small program designed to act as a "splash screen"
for TaystJK.

!["Checking for updates" animated preview](doc/checking-for-updates.avif)

It will check whether an update is available for TaystJK, download it
and install it (if there is indeed a new version), and then launch the game.

This project does not include any code nor binaries from TaystJK. Instead,
the updater downloads the latest release from the official repository:
https://github.com/taysta/TaystJK

> ℹ️ **TaystJK** is an Open Source multiplayer client for **Star Wars Jedi Knight: Jedi Academy**,
> and is described by Tayst as an "effort to maintain and improve EternalJK".


## Requirements

- A **TaystJK** installation  
- **.NET Framework 4.8**, which is preinstalled on Windows 10 and 11  


## Installation

1. Drop `TaystJK Updater.exe` into the same folder as `taystjk.x86_64.exe`
   or `taystjk.x86.exe`.
2. Make sure the folder is **not** admin-protected.  
   - Running the updater as admin is ⚠️ **highly discouraged** ⚠️,
     as it would then launch TaystJK with admin rights.
3. You can then pin `TaystJK Updater.exe` to your start menu,
   or send it to your desktop as a shortcut.  
   - You can rename the shortcut to just `TaystJK`, if you want.  
   - You could even edit the shortcut icon, and select `taystjk.x86_64.exe`
     (or `taystjk.x86.exe`) as the source for the icon.  


## More info

- [Advanced usage](doc/advanced-usage.md)
- [Technical choices and assumptions](doc/technical-info.md)


## License

\[TODO\]


## Credits

**Slash:** Updater code, background image¹, icons² and animations.

1. Background image adapted from my
   ["widescreen levelshots"](https://jkhub.org/files/file/4179-widescreen-levelshots/) pack  
   → Recreation of the original `mp/ffa4` levelshot

2. "Jedi Knight" logo loosely based on:  
   https://en.wikipedia.org/wiki/File:Star_Wars_Jedi_Knight_II_Jedi_Outcast_Logo.svg
   
**TaystJK Updater** also includes the
[LightJson](https://github.com/MarcosLopezC/LightJson) library (MIT-licensed), from Marcos López C.
([Why?](docs/technical-info.md#lightjson))
