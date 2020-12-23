# Small utility to grab File-Access-Errors and unhandled Exceptions for dotnet core

this code is just a small tool that will use the createdump-utility from .net to fetch full dumps from a 
running .net core-process if an unhandled Exception or a special IO-Exception happens

### Building

just use 
```shell
dotnet publish -c release
```
and you will find the contents in ``bin\release\netcoreapp3.0\publish\``.

### Injecting into the observed process

To do so you need to use the process-hooking from dotnetcore.

Please set the Environment:

```shell
DOTNET_STARTUP_HOOKS=<directory_where_the_tool_is_deployed>/ExceptionGrabber.dll
```

after starting the process again you should notice a line like:

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

