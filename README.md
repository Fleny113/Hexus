# Hexus

[![.NET build status](https://github.com/Fleny113/Hexus/actions/workflows/dotnet.yml/badge.svg?branch=main&event=push)](https://github.com/Fleny113/Hexus/actions/workflows/dotnet.yml)
![](https://img.shields.io/badge/.NET-9.0-purple)

Hexus is a process manager built using .NET 9 designed to work on Linux and Windows seamlessly while being nice and simple to use

## Features

- Performant
- Supports sending CTRL + C (SIGINT) signals on both Linux and Windows
- All the logs are in a single place ready to be read with timestamps and type of output
- Keeps track of the complete usage of resources of an application, including child processes
- Has a nice and simple CLI to use to manage all your applications
- Can autogenerate the startup scripts for you to customize based on your needs for Windows (Windows Task Scheduler) and Linux (systemd)
- Exposes both socket and (optional) HTTP port for the requests to the daemon, _under windows sockets are supported_

## Installation

Download the binary from the latest CI release below or compile it using the [`.NET 9`](https://get.dot.net/9) SDK.

|      OS       |                                                                                                     Self-contained                                                                                                     |                                                                                            Runtime dependent                                                                                             |
|:-------------:|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| Windows amd64 |     [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-x64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-x64-self-contained.tar.gz)     |     [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-x64-runtime.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-x64-runtime.tar.gz)     |
|  Linux amd64  |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-x64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/linux-x64-self-contained.tar.gz)   |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-x64-runtime.tar.gz) \| [CI build](https://github.com/fleny113/Hexus/releases/download/ci/linux-x64-runtime.tar.gz)   |
| Windows arm64 |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-arm64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-arm64-self-contained.tar.gz)   |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-arm64-runtime.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-arm64-runtime.tar.gz)   |
|  Linux arm64  | [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-arm64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/linux-arm64-self-contained.tar.gz) | [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-arm64-runtime.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/linux-arm64-runtime.tar.gz) |

### Compilation

If you want to compile the binary for yourself you need to install:

- [`.NET 9`](https://get.dot.net/9) SDK
- `ASP.NET Core`, usually bundled with the SDK

To create a release build to use run the following command after have cloned the repo and being in the top directory

```sh
dotnet publish Hexus
```

Optionally you can add `--self-contained` to remove the need for the .NET Runtime to be installed or with the `--runtime` flag to specify a target runtime like `linux-arm64` or `win-x64`

## Usage

#### Start the daemon

Hexus requires you to start the daemon manually before you can start using it. To start the daemon run the command `hexus daemon start` and if you want to stop it without sending a CTRL + C or a kill signal to the process you can use the `hexus daemon stop` command.

If you want to add the Hexus daemon to the startup you can use the `hexus startup` command that will detect what platform you are on and give you a powershell script for the windows task scheduler when run under windows and a systemd unit service file when running under Linux to quickly set up the startup process.

> [!TIP]
> When the command of `hexus startup` is redirected it won't output the decorations around the text to be easier to use the script/service that it creates

> [!NOTE]
> Hexus only supports Windows task scheduler and systemd unit services as startup scripts so using another platform will require you to set it up manually.

#### New application

Creating a new application is really easy: Just give your application a name and then type the command to run it just as normal and optionally add flags

```sh
hexus new <name> <executable> [<arguments>] [<flags>]
```

All the flags are available in the help for the command, you can use the `--help` or `-h` flag to see it.

#### List applications

To list all the application currently running you can use the list command

```sh
hexus list
```

The list command will provide some basic information on the application, but you can use the `hexus info` command with the name of the application to get some more info on it

#### Get applications logs

To read all the application logs (By default stored under `~/.local/state/hexus/logs/<application name>.log`) you can use the logs command like in the example:

```sh
hexus logs <application name>
```

##### Flags

- `--lines` or `-l` to specify the number of log lines to fetch from the log file, specify -1 to get all lines
- `--no-streaming` to disable the streaming of logs to the console while the command is active
- `--no-dates` to disable the Hexus provided timestamp of the log lines
- `--current` or `-c` to show only the logs from the currently running for last execution of the application.
- `--after` or `-a` to select logs that have a timestamp after the one provided (does get affected by `--timezone`)
- `--before` or `-b` to select logs that have a timestamp before the one provided (does get affected by `--timezone`)
- `--timezone` timezone of the Hexus provided timestamps, should be picked from the system-provided timezones. Defaults to the computer current timezone.

All the flags are available in the help for the command, you can use the `--help` or `-h` flag to see it.

##### Log file

If you want to manually parse the log files the format is as follows: `[<date>,<type>] <message>` where

- `date` is a date in UTC time using the ISO8601 format
- `type` is one of `STDOUT`, `STDERR` or `SYSTEM`, with `SYSTEM` being used for Hexus messages like the application start or stop while `STDOUT` and `STDERR` for the actual logs of the application
- `message` is the actual message the application logged to the console

#### Start / Stop / Restart / Delete application

To start an application you can use the `hexus start <name>` command with the name right after and to stop an application you can use the `hexus stop <name>` command with the name right after, for the stop command you can also specify the `--force` flag what will kill as soon as possible the application without sending a CTRl + C.

Similar to the stop command you can also restart an application with the name of it using the `hexus restart <name>` command with, if wanted, the `--force` flag to force the stop of the application

If you don't want to have an application you can use the `hexus delete <name>` command to remove it from the applications. This command also supports the `--force` flag to stop the application by force

All the flags are available in the help for the command, you can use the `--help` or `-h` flag to see it.

> [!WARNING]
> When deleting an application the log file will also be deleted

#### Edit application

To edit an application you will first need to stop it using the `hexus stop` command, and then you can change add the different options for it, check the `--help` for all the flags.

#### Send input to the application

Hexus also allows sending messages in the application `STDIN` by using the `hexus info <name> <message>` command where name is the application name and message whatever you need to send to the application.

Keep in mind Hexus will send the message to the direct child so in a situation where the direct child is not the application you want to send the input to you might have troubles

## Configuration

Hexus will store the configuration in `$XDG_CONFIG_HOME/hexus.yaml`,
the daemon socket on `$XDG_RUNTIME_DIR/hexus.sock` for Linux and `$XDG_STATE_HOME/hexus/hexus.sock` for Windows,
the logs for the daemon in `$XDG_STATE_HOME/hexus/daemon.log` and the logs for the applications in `$XDG_STATE_HOME/hexus/applications/<app name>.log`

These locations can be customized with the `XDG_CONFIG_HOME` (defaults to `~/.config`), `XDG_RUNTIME_DIR`[^XDG_RUNTIME_DIR] and `XDG_STATE_HOME` (defaults to `.local/state`) environment variables.
On Windows setting the `XDG_RUNTIME_DIR` will not be ignored and that path will be used instead of using the `$XDG_STATE_HOME/hexus` folder.

[^XDG_RUNTIME_DIR]: `XDG_RUNTIME_DIR` does not provide a clear default, however if running on Windows the value "defaults" to `$XDG_STATE_HOME/hexus`, on Unix systems a `<UID>-runtime` directory, where `<UID>` is replaced with the user ID that is running hexus, will be created in the temp with the permissions `700` and the current user as the owner according to the XDG basedir specification

The config file is a `.yaml` file with the following options:

- `unixSocket`: Changes where the socket is located. Used for connecting to the daemon.
- `httpPort`: The HTTP port to listen as an addition way to access the daemon to the required socket, useful for interfacing with software that cant use the socket. \[OPTIONAL\]
- `cpuRefreshIntervalSeconds`: The interval for the refresh of CPU usage of applications. The lower the value, the more CPU Hexus will use but the more precise it will the CPU usage of applications. Default 2.5 seconds.
- `applications`: Object with the application name as the key as the value the following proprieties.
  - `executable`: The file to execute when spawning the application.
  - `arguments`: The arguments to give the executable, as a string. \[OPTIONAL\]
  - `workingDirectory`: The directory where the application should start.
  - `status`: Status of the application. Possible values: `Crashed`, `Exited`, `Running` (Matches the `HexusApplicationStatus` enum)
  - `note`: A note that can be seen in the `info` command. \[OPTIONAL\]
  - `environmentVariables`: All the environment variables for the application. Application **WILL NOT** inherit the environment variables from the daemon \[OPTIONAL\]

> [!NOTE]
> The configuration file, the socket and the daemon log will have a `.dev` suffix before the extension to the name when running in development.
> The resulting config file, socket file and log file are respectively `hexus.dev.yaml`, `hexus.dev.sock` and `daemon.dev.log`

#### PM2 Migration

Hexus allows you to migrate your current pm2 applications saved in the `dump.pm2` file. You can use the `migrate-pm2` command with, optionally, the `--pm2-dump` option in case you are not using the default `$HOME/.pm2/dump.pm2` file, just remember to run `pm2 save` before you run the command.

> [!WARNING]
> Hexus only supports migrating from pm2 `5.3.0`, using another version might give errors. Migrating apps that are configured as cluster in pm2 will fail and Hexus will skip them as Hexus supports `fork_mode` only.
>
> Hexus uses names to discriminate on what application the operation should be taken, for this reason if there are name conflicts with exiting application Hexus will try to save the application with the same name, if that fails due to an application having the same name Hexus will add the `-pm2` suffix. If even at that point there were conflicts with exiting application, Hexus will log out on what application it failed for you to configure it manually

## Roadmap

- Add log rotation support

## Limitations

- MacOS is not supported as Hexus needs to get the child processes for an application to calculate the correct RAM and CPU usages, and I don't have anything to test how to get them.

## License

Hexus is under the [MIT license](./LICENSE.md)
