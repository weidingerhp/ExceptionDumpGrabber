# Small utility to grab memory dumps on first-chance and unhandled exceptions

This code is just a small tool that will use the createdump utility from .NET to fetch full dumps from a
running .NET process if one of the configured first-chance exception or an unhandled exception happens.

### Building

Just use
```shell
dotnet publish -c release
```
and you will find the contents in ``bin\release\netcoreapp3.0\publish\``.

### Injecting into the observed process

To do so you need to use the .NET start-up hooks.

Please set the Environment:

```shell
DOTNET_STARTUP_HOOKS=<directory_where_the_tool_is_deployed>/ExceptionGrabber.dll
DT_FIRST_CHANCE_EXCEPTIONS=<ExceptionA>,<ExceptionB>
```

After starting the process again you should notice a line like:

```shell
...
Exception Grabber active - wrtiting dumps to /tmp
...

```

### Settings

Setting the directory dumps will be written to
```shell
DT_CRASH_DUMP_DIR=/home/user/mycrashdumps
```
Changing the crashdump - executable
```shell
DT_DUMP_EXEC=/root/.dotnet/shared/Microsoft.NETCore.App/3.1.4/createdump
```
