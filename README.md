# Hexus

[![.NET build status](https://github.com/Fleny113/Hexus/actions/workflows/dotnet.yml/badge.svg?branch=main&event=push)](https://github.com/Fleny113/Hexus/actions/workflows/dotnet.yml)
![](https://img.shields.io/badge/.NET-10.0-purple)

Hexus is a process manager built using .NET 10 designed to work on Linux and Windows seamlessly while being nice and simple to use

## Features

- Performant
- Supports sending CTRL + C (SIGINT) signals on both Linux and Windows
- All the logs are in a single place ready to be read with timestamps and type of output
- Keeps track of the complete usage of resources of an application, including child processes
- Has a nice and simple CLI to use to manage all your applications
- Can autogenerate the startup scripts for you to customize based on your needs for Windows (Windows Task Scheduler) and Linux (systemd)
- Exposes both socket and (optional) HTTP port for the requests to the daemon, _under windows sockets are supported_

<!-- TODO: When ready to release 0.6 add a note noting that the config has to be migrated -->

## Installation

Download the binary from the latest CI release below or compile it using the [`.NET 10`](https://get.dot.net/10) SDK.

|      OS       |                                                                                                     Self-contained                                                                                                     |                                                                                            Runtime dependent                                                                                             |
|:-------------:|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| Windows amd64 |     [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-x64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-x64-self-contained.tar.gz)     |     [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-x64-runtime.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-x64-runtime.tar.gz)     |
|  Linux amd64  |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-x64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/linux-x64-self-contained.tar.gz)   |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-x64-runtime.tar.gz) \| [CI build](https://github.com/fleny113/Hexus/releases/download/ci/linux-x64-runtime.tar.gz)   |
| Windows arm64 |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-arm64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-arm64-self-contained.tar.gz)   |   [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/win-arm64-runtime.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/win-arm64-runtime.tar.gz)   |
|  Linux arm64  | [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-arm64-self-contained.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/linux-arm64-self-contained.tar.gz) | [Latest Release](https://github.com/fleny113/Hexus/releases/latest/download/linux-arm64-runtime.tar.gz) \| [CI Build](https://github.com/fleny113/Hexus/releases/download/ci/linux-arm64-runtime.tar.gz) |

### Compilation

If you want to compile the binary for yourself you need to install:

- [`.NET 10`](https://get.dot.net/10) SDK
- `ASP.NET Core`, usually bundled with the SDK

To create a release build to use run the following command after have cloned the repo and being in the top directory

```sh
dotnet publish Hexus
```

Optionally you can add `--self-contained` to remove the need for the .NET Runtime to be installed or with the `--runtime` flag to specify a target runtime like `linux-arm64` or `win-x64`

## Usage

#### Start the daemon

To start the daemon run `hexusd`. To stop the daemon you can terminate the process with CTRL-C or a signal or you can use the `hexus daemon stop` command.

If you want to add `hexusd` to the startup you can use the `hexus startup` command. It will detect what platform you are on and give you a powershell script for the windows task scheduler when run under windows or a systemd unit service file when running under Linux to quickly set up the startup process.

> [!TIP]
> When the command of `hexus startup` is redirected it won't output the decorations around the text to be easier to use the script/service that it creates

> [!NOTE]
> Hexus only supports Windows task scheduler and systemd unit services as startup scripts so using another platform will require you to set it up manually.

#### New application

To create a new application you can use the `new` command or create the `appName.toml` file in the applications/ directory in the hexus config directory.

```sh
hexus new <name> <exe> [... <args>] [<flags>]
```

After you create the application you will be asked if you want to open the configuration in the editor: not all configurations are available as flags to the `new` command, only the most common are.
Additionally if the daemon is running you will be also asked if you want to reload the configuration and load the new app.

For more details on the application toml configuration see [the configuration section below](#configuration).

#### List applications

To list all the application currently configured you can use the `list` command

```sh
hexus list
```

The list command will provide some basic information on the application, but you can use the `info` command with the name of the application to get some more info on it.

Both `list` and `info` can work without the daemon, in which case they will read the configuration and not provide any state of the apps.
If the daemon hasn't been reloaded and the configuration has changed, both `list` and `info` will still output the daemon-provided data when using the daemon.

#### Get applications logs

To read all the application logs you can use the logs command like in the example:

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

The log file is placed in `$XDG_STATE_HOME/hexus/logs/<app name>.log`, see [configuration](#configuration) for more details.

If you want to manually parse the log files the format is as follows: `[<date>,<type>] <message>` where

- `date` is a date in UTC time using the ISO8601 format
- `type` is one of `STDOUT`, `STDERR` or `SYSTEM`. `SYSTEM` only used for Hexus messages like the application start or stop.
- `message` is the application log.

#### Start / Stop / Restart

Hexus has a `enabled` config for application and a `state` for applications.

An application is `enabled` is the daemon should start it when the daemon itself starts, an application state is running if the process for the application has been spawned.

To start an application you can use the `start <name>` command, to stop an application you can use the `stop <name>`, to restart an application you can use `restart <name>`.

`hexus stop` and `hexus restart` allow for the `--force` flag to be passed to bypass the CTRL+C signal and wait of up to 30s and simply kill the application immediately.

#### Delete

You can delete an application with the `delete` command or manually deleting the application's configuration file.

`hexus delete` also allow for the `--force` flag to be passed to bypass the CTRL+C signal and wait of up to 30s and simply kill the application immediately.

> [!WARNING]
> By default, when deleting an application the log file will also be deleted, you can use `--keep-logs` to keep it.

#### Edit application

Editing an application with `edit` will open up an editor for you to edit the configuration directly.

After you save and quit the CLI will ask you if you want to reload to apply the changes.

#### Reload

If you edit the configuration you can apply the changes with the `reload` command.

#### Send input to the application

Hexus also allows sending messages in the application `STDIN` by using the `input <name> <message>` command.

You need to specify the application name and the message you want to send to the application.

You can add the `--no-new-line` (`-n`) flag to avoid adding the newline at the end.

Hexus will always send the message to the direct child, if the application spawns child-processes you won't be able to write to their `STDIN` unless the application itself handles that.

## Configuration

Hexus configuration is stored in `$XDG_CONFIG_HOME/hexus`.

The socket the daemon exposes is on `$XDG_RUNTIME_DIR/hexus/daemon.sock` for Linux and `$XDG_STATE_HOME/hexus/daemon.sock` for Windows,

The logs for the daemon in `$XDG_STATE_HOME/hexus/daemon.log` and the logs for the applications in `$XDG_STATE_HOME/hexus/logs/<app name>.log`

The state for the apps is stored in `$XDG_STATE_HOME/hexus/states/<app name>.state`.

These locations can be customized with the `XDG_CONFIG_HOME` (defaults to `~/.config`), `XDG_RUNTIME_DIR`[^XDG_RUNTIME_DIR] and `XDG_STATE_HOME` (defaults to `.local/state`) environment variables.

On Windows setting the `XDG_RUNTIME_DIR` will not be ignored and that path will be used instead of using the `$XDG_STATE_HOME/hexus` folder.

[^XDG_RUNTIME_DIR]: `XDG_RUNTIME_DIR` does not provide a clear default, however if running on Windows the value "defaults" to `$XDG_STATE_HOME/hexus`, on Unix systems a `hexus-<UID>` directory, where `<UID>` is replaced with the user ID that is running hexus, will be created in the temp directory with the permissions `700` and the current user as the owner according to the XDG basedir specification

The configuration is split in multiple files in the TOML format.

Intervals can be specified with the following format: `[<hours>h] [<minutes>m] [<seconds>s]`. Example: `5s`
Byte sizes can be specified with the following format: `<size>[unit]B`. Example: `5GB`

> [!NOTE]
> The configuration directory and state directory will have a `.dev` suffix when run in development. The `.dev` suffix is enabled if `DOTNET_ENVIRONMENT` is `Development`

### `daemon.toml`

This is the configuration for the daemon

| Name                      | Description                                                                  |
|:--------------------------|:-----------------------------------------------------------------------------|
| `unix-socket`             | Path for the unix socket, used for connecting to the daemon                  |
| `http-port`               | Optional http port to expose the daemon                                      |
| `cpu-polling-interval`    | Interval for polling the CPU usage of applications                           |
| `memory-polling-interval` | Interval for polling the Memory usage of applications for the memory limiter |
| `memory-limit`            | The default limit in byte size for application memory usage                  |

### `applications/<name>.toml`

This is the configuration for a single application.

| Name           | Description                                                     |
|:---------------|:----------------------------------------------------------------|
| `exe`          | Path for the executable of the application                      |
| `args`         | Optional arguments for the executable                           |
| `working-dir`  | Path for the working directory of the application               |
| `enabled`      | Whatever the application should start when the daemon starts    |
| `note`         | Optional user-defined string. It is printed in the info command |
| `memory-limit` | The limit in byte size for application memory usage             |
| `env`          | Optional TOML dictionary for envs                               |

### State file

This is the persistant data for an application. This is also a TOML file.

| Name      | Description                                                                                               |
|:----------|:----------------------------------------------------------------------------------------------------------|
| `crashed` | Indicates if the application has crashed. A crashed app is not restarted at daemon start even if enabled. |

### Old hexus config (.yaml based config)

Old versions of hexus (< 0.6) used a .yaml file stored at `$XDG_CONFIG_HOME/hexus.yaml`.

This format is no longer accepted, however you can use the `migrate` command to update your configuration from the old single-file .yaml based file to the new multi-file .toml configuration.

Additionally old versions defaulted to capture the entire env of the shell when `hexus new` was executed as used that as the env for the new app. This is no longer the case as it polluted application envs with junk/transient environment variables.

<!-- TODO: Env configs will get a rework soon, update docs when that happens -->

### PM2 Migration

Hexus allows you to migrate your current pm2 applications saved in the `dump.pm2` file. You can use the `migrate-pm2` command with, optionally, the `--pm2-dump` option in case you are not using the default `$HOME/.pm2/dump.pm2` file, just remember to run `pm2 save` before you run the command.

> [!WARNING]
> The only tested version for migration is pm2 `5.3.0` using another version might give errors. Migrating apps that are configured as cluster in pm2 will fail and Hexus will skip them as Hexus supports `fork_mode` only.
>
> Hexus uses names to discriminate on what application the operation should be taken, for this reason if there are name conflicts with existing application Hexus will add the `-pm2` suffix. If the name is still used, it will add a `-pm2-[num]` starting from 2 until the name conflict is solved.

## Roadmap

- Add log rotation support

## Limitations

- MacOS is not supported as Hexus needs to get the child processes for an application to calculate the correct RAM and CPU usages, and I don't have anything to test how to get them.

## License

Hexus is under the [MIT license](./LICENSE.md)
