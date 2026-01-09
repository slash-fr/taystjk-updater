# Advanced usage

## "Path to TaystJK" argument

You can specify the (absolute or relative) path to the TaystJK executable
as a command-line argument, or as a shortcut "target" argument.

For instance:
```
.\Updater.exe taystjk.x86_64.exe
```

This enables you to:
- Disambiguate between the 64-bit version and the 32-bit version,
  if you happen to have both in the same folder
- Start the updater from a separate folder from the main executable


## Other arguments

Any other arguments will get passed to TaystJK.

So, as an example:
```
.\Updater.exe set r_mode 6 + connect 192.0.2.1
```
would launch the game with a resolution of `1024x768` and immediately connect
to the specified server.


## Both types of arguments

You can combine both the executable name (or path) and additional params:
```
.\Updater.exe taystjk.x86.exe connect 192.0.2.1
```
In this case, the "path to TaystJK" argument must come first.
