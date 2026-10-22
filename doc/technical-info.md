# Technical info

## Why C#, WPF, and .NET Framework 4.8?

I opted for technologies that are easy to use, allow for extensive UI customization, and generate small binaries.

The project specifically targets **.NET Framework 4.8**, which is pre-installed on Windows 11
(and Windows 10 since the May 2019 Update).
That's how the release can be so small, without requiring an additional download.


## Why LightJson?

**TaystJK Updater** uses the
[LightJson](https://github.com/MarcosLopezC/LightJson) library from Marcos López C.

**Q:** Why not simply use [System.Text.Json](https://www.nuget.org/packages/System.Text.Json)?  
**A:** Because it comes with a bunch of DLL files, adding another megabyte to the release size.
I really wanted the updater to be **self-contained**, and as small as (reasonably) possible.

   
## Assumptions

The updater relies on a number of assumptions, that make it admittedly quite brittle.
They are listed here to facilitate future maintenance:

1. The [https://github.com/taysta/TaystJK](https://github.com/taysta/TaystJK) repository exists.

2. Said repository has a release tagged as `latest-build`.

3. The release has `TaystJK-windows-x86.zip` and `TaystJK-windows-x86_64.zip` assets (among others).

4. The release ZIP has its content at the root of the archive, with no intermediate subdirectory.

5. The main executable is named `taystjk.x86_64.exe` (64-bit) or `taystjk.x86.exe` (32-bit).

6. The install folder is not admin-protected.

7. Optional: The main exe contains an ASCII string in the following format:
   ```
   TaystJK: latest-build-<hash><separator>
   ```
   anywhere in the binary content, where `<hash>` consists of 4 to 40 hexadecimal characters,
   and `<separator>` is either a space, `\n` or `\0`.  
    
   And `<hash>` refers to **same** commit as the `target_commitish` of its release in the GitHub API.  
   
   Yikes!  
   Fortunately, the updater can still detect an outdated version even if that string isn't present
   (by comparing file timestamps).


## Assumptions about the updater itself

The updater can update itself.
For this feature to work properly, it requires the following conditions:

1. The [https://github.com/slash-fr/taystjk-updater](https://github.com/slash-fr/taystjk-updater) repository exists.

2. Said repository has at least 1 non-prerelease, non-draft release.

3. The latest release has a `TaystJK_Updater.exe` asset.

4. `TaystJK_Updater.exe` contains a `GitVersion` resource with a commit hash (4 to 40 hexadecimal characters)
   that refers to the **same** commit as the `target_commitish` of its release in the GitHub API.

   > **Note to self:** When publishing a new release, explicitly select a commit as the target.
   > Not just `main`, or the updater will keep updating itself.

5. The folder containing the updater is not admin-protected.
