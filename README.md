# AvaloxServer

> **Project Status:** Completed academic coursework project. Archived and no longer actively maintained.

The backend TCP server for the [Avalox](https://github.com/wellmq/Avalox) messaging client, developed as the server component for a college coursework project.

## Overview

AvaloxServer listens for incoming TCP client connections, handles authentication and message routing, and stores persistent data in a local SQLite database.

- **Runtime:** .NET 10.0 (C# 13)
- **Networking:** Async sockets via `TcpListener` with per-client `System.Threading.Channels` queues
- **Database:** SQLite with [Dapper](https://github.com/DapperLib/Dapper) Micro-ORM (`Main.db`)
- **Security:** Salted PBKDF2 (SHA-256) password hashing

## Network Protocol

The server communicates over a binary framing protocol:

$$\text{[ 1 byte: Type ]} + \text{[ 4 bytes: Length ]} + \text{[ JSON Payload ]}$$

| Code | Type | Payload | Description |
|:---:|:---|:---|:---|
| `0` | Registration | `Credentials` | Hashes password and creates user account |
| `1` | Authentication | `Credentials` | Verifies credentials and establishes session |
| `2` | Send Message | `Message` | Saves message to database |
| `3` | Sync Messages | `LastMessageInfo` | Fetches new messages since last known ID |
| `4` | Online Status | `TargetUser` | Checks if a user is currently connected |

## Build & Run

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Run from Source

```bash
git clone https://github.com/wellmq/AvaloxServer.git
cd AvaloxServer
dotnet run
```

By default, the server binds to port `7777`. To specify a custom port:

```bash
dotnet run -- 8080
```

### Build Release Binary

```bash
dotnet build -c Release
```

The compiled binary will be located in `bin/Release/net10.0/`.

## License

[MIT](LICENSE)
