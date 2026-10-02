# ⚡ Avalox Server

<div align="center">

[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://dotnet.microsoft.com/)
[![.NET 10](https://img.shields.io/badge/.NET_10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![Dapper](https://img.shields.io/badge/ORM-Dapper-orange?style=for-the-badge)](https://github.com/DapperLib/Dapper)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

**TCP messaging server for Avalox, written in C# (.NET 10) with SQLite storage.**

🌐 **English** • [Русский](README.ru.md)

[Features](#-features) • [Tech Stack](#-tech-stack) • [Architecture](#-architecture) • [Network Protocol](#-network-protocol) • [Quick Start](#-quick-start) • [Security](#-security--reliability) • [License](#-license)

</div>

---

## ✨ Features

- 🚀 **Asynchronous Sockets**: Handles client connections asynchronously with `async/await` on `TcpListener`.
- 🔀 **Response Queues**:
  - Each client connection uses a `Channel<Response>` queue.
  - Sends responses sequentially so packets don't overlap.
- ⚙️ **Configurable Port with Auto-Fallback**:
  - Launch on any port via CLI arguments (`-p 8080`, `--port 9000`, or positional argument).
  - Port collision detection: if the requested port is occupied, the server automatically scans and binds to the next available port.
- 🔐 **Password Hashing**:
  - Passwords are saved as salted PBKDF2 (SHA-256) hashes.
  - Plaintext passwords are never stored in the database.
- 💾 **SQLite Storage via Dapper**:
  - Local database `Main.db` with schema auto-initialization (`Users` and `Messages` tables) on first launch.
  - Fast, type-safe queries powered by the Dapper Micro-ORM.
- 🧹 **Dead Connection Watchdog**:
  - Periodic background timer purges timed-out or abandoned sockets every 3 seconds.
  - Detailed console logging for client connections, disconnections, and timeouts with IP addresses.

---

## 🛠 Tech Stack

- **Platform**: [.NET 10.0](https://dotnet.microsoft.com/) (C# 13)
- **Networking**: `System.Net.Sockets`, `System.Threading.Channels`
- **Database**: [SQLite](https://www.sqlite.org/) (`Microsoft.Data.Sqlite 10.0`)
- **Micro-ORM**: [Dapper 2.1](https://github.com/DapperLib/Dapper)
- **Cryptography**: `System.Security.Cryptography` (PBKDF2 / SHA256)
- **Serialization**: `System.Text.Json`

---

## 🏛 Architecture

```
AvaloxServer/
├── Models/
│   ├── Connection.cs       # Client connection handler and packet write queue
│   ├── Credentials.cs      # User credentials DTO
│   ├── HashSalt.cs         # Database password hash and salt model
│   ├── LastMessageInfo.cs  # Message synchronization request DTO
│   ├── Message.cs          # Chat message DTO
│   ├── Response.cs         # Unified server response DTO
│   └── TargetUser.cs       # Target user online status query DTO
├── Services/
│   ├── Database.cs         # Automatic SQLite database and schema initialization
│   ├── MessageStorage.cs   # Message persistence and retrieval via Dapper
│   ├── RegAuth.cs          # Registration, authentication, and PBKDF2 hashing
│   └── Server.cs           # TCP listener, active client pool, and watchdog
├── Program.cs              # Entry point and CLI port argument parser
└── AvaloxServer.csproj     # Project configuration and dependencies
```

---

## 📡 Network Protocol

The server communicates via TCP using explicit binary packet framing:

$$\text{[ 1 byte: Type ]} + \text{[ 4 bytes: Int32 Payload Length ]} + \text{[ N bytes: JSON Payload ]}$$

### Routing Table:
| Code | Request Type | Payload | Processing Logic | Result |
|:---:|:---|:---|:---|:---|
| `0` | **Registration** | `Credentials` | Generate salt, hash password with PBKDF2, persist to DB | `Response(true/false)` |
| `1` | **Authentication** | `Credentials` | Verify password hash, bind username to connection | `Response(true/false)` |
| `2` | **Send Message** | `Message` | Validate session, persist message to SQLite with timestamp | `Response(true/false)` |
| `3` | **Sync Messages** | `LastMessageInfo` | Fetch messages where `Id > lastId` for authenticated user | `Response(List<Message>)` |
| `4` | **Online Status** | `TargetUser` | Check active connections pool for target username | `Response("yes"/"no")` |

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone & Build
```bash
git clone https://github.com/wellmq/AvaloxServer.git
cd AvaloxServer
dotnet build
```

### Starting the Server
**1. Default port (`7777`):**
```bash
dotnet run
```

**2. Custom port via flags:**
```bash
dotnet run -- -p 8888
# or
dotnet run -- --port 9000
```

**3. Custom port as positional argument:**
```bash
dotnet run -- 5555
```

---

## 🔒 Security & Reliability
 
- **Password Hashing**: Passwords are saved with a salt using PBKDF2 instead of plain text.
- **Packet Size Limit**: 5 MB upper limit per packet to reject oversized payloads.
- **Response Queues**: Responses are sent sequentially through channels so packets don't overlap.
- **Auto Schema Init**: Database tables (`Users`, `Messages`) are created automatically on startup.

---

## 🔗 Related Project

- **Client**: [Avalox Client](https://github.com/wellmq/Avalox) — Cross-platform desktop messenger GUI built on Avalonia UI.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
