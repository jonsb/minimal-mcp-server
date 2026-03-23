# Minimal MCP Server (C#, stdio)

A from-scratch implementation of an [MCP](https://modelcontextprotocol.io) server for learning purposes.  
No SDK. No HTTP. Just raw JSON-RPC 2.0 over stdin/stdout.

---

## How MCP stdio transport works

MCP uses **newline-delimited JSON** — every message is a single JSON object on one line:

```
stdin  → messages arriving from the client (e.g. Copilot CLI)
stdout → responses sent back to the client
stderr → safe for logging; the client ignores it
```

---

## The handshake (in order)

```
Client                          Server
  |                               |
  |-- initialize ---------------→ |   "Hi, here's my info and capabilities"
  |← initialize response -------- |   "Hi, here's what I can do"
  |                               |
  |-- notifications/initialized → |   "Got it, handshake done" (no reply expected)
  |                               |
  |-- tools/list ---------------→ |   "What tools do you have?"
  |← tools/list response -------- |   "Here's my list of tools"
  |                               |
  |-- tools/call ---------------→ |   "Please run tool X with these arguments"
  |← tools/call response -------- |   "Here's the result"
```

---

## JSON-RPC 2.0 basics

Every message has this shape:

```json
{ "jsonrpc": "2.0", "id": 1, "method": "...", "params": { ... } }
```

- **Requests** have an `id` → you must send a response with the same `id`
- **Notifications** have no `id` → you must NOT respond
- **Responses** have an `id` + either `"result"` or `"error"`

---

## Try it manually

Build and run the server, then type messages into stdin:

```bash
dotnet run
```

Paste these lines one at a time (each is one JSON message):

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}
{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"message":"Hello, MCP!"}}}
```

---

## Register with Copilot CLI

Add this to your MCP config (`/mcp` → add server):

```json
{
  "command": "dotnet",
  "args": ["run", "--project", "C:\\mobitech\\minimal-mcp-server"]
}
```

Or after publishing:

```json
{
  "command": "C:\\mobitech\\minimal-mcp-server\\bin\\Release\\net10.0\\MinimalMcpServer.exe"
}
```
