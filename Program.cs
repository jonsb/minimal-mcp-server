using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// MCP uses newline-delimited JSON over stdio.
// Every message is a JSON-RPC 2.0 object on a single line.
// stdin  → incoming messages from the MCP client (e.g. Copilot CLI)
// stdout → outgoing responses / notifications to the client
// stderr → safe for logging; the client ignores it

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

Log("Minimal MCP server started (stdio transport)");

while (Console.ReadLine() is { } line)
{
    if (string.IsNullOrWhiteSpace(line)) continue;

    JsonNode? msg;
    try { msg = JsonNode.Parse(line); }
    catch { Log($"Could not parse JSON: {line}"); continue; }

    if (msg is null) continue;

    // JSON-RPC 2.0: requests have an "id", notifications do not.
    var id = msg["id"];
    var method = msg["method"]?.GetValue<string>() ?? "";
    var @params = msg["params"];

    //Log($"← {method} (id={id})");

    // Notifications have no "id" — we must NOT reply to them.
    bool isNotification = id is null;

    switch (method)
    {
        case "initialize":
            // The client introduces itself and asks what this server can do.
            // We reply with our protocol version, server name, and declared capabilities.
            Reply(id!, new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject
                {
                    // We support tools — that's the only capability we implement.
                    ["tools"] = new JsonObject()
                },
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = "minimal-mcp-server",
                    ["version"] = "1.0.0"
                }
            });
            break;

        case "notifications/initialized":
            // The client confirms it received our initialize response.
            // This is a notification — no reply allowed.
            Log("Handshake complete");
            break;

        case "tools/list":
            // The client wants to know what tools we expose.
            // Each tool needs a name, description, and a JSON Schema for its arguments.
            Reply(id!, new JsonObject
            {
                ["tools"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"]        = "echo",
                        ["description"] = "Returns the message you send it. Useful for testing.",
                        ["inputSchema"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["message"] = new JsonObject
                                {
                                    ["type"]        = "string",
                                    ["description"] = "The text to echo back"
                                }
                            },
                            ["required"] = new JsonArray { "message" }
                        }
                    },
                    new JsonObject
                    {
                        ["name"]        = "add",
                        ["description"] = "Returns the sum of the numbers you send it.",
                        ["inputSchema"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["number1"] = new JsonObject
                                {
                                    ["type"]        = "number",
                                    ["description"] = "The first number"
                                },
                                ["number2"] = new JsonObject
                                {
                                    ["type"]        = "number",
                                    ["description"] = "The second number"
                                }
                            },
                            ["required"] = new JsonArray { "number1", "number2" }
                        }
                    }
                }
            });
            break;

        case "tools/call":
            {
                // The client wants to invoke a specific tool.
                var toolName = @params?["name"]?.GetValue<string>() ?? "";
                var arguments = @params?["arguments"];

                if (toolName == "echo")
                {
                    var message = arguments?["message"]?.GetValue<string>() ?? "";
                    //Log($"echo(\"{message}\")");

                    // Tool results are wrapped in a "content" array.
                    // Each item in the array is a content block with a type.
                    // "text" is the most common type.
                    Reply(id!, new JsonObject
                    {
                        ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = message
                        }
                    }
                    });
                }
                else if (toolName == "add")
                {
                    double number1 = arguments["number1"].GetValue<double>();
                    double number2 = arguments["number2"].GetValue<double>();

                    double sum = number1 + number2;

                    // Tool results are wrapped in a "content" array.
                    // Each item in the array is a content block with a type.
                    // "text" is the most common type.
                    Reply(id!, new JsonObject
                    {
                        ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = sum.ToString()
                        }
                    }
                    });
                }
                else
                {
                    // Unknown tool — return a JSON-RPC error.
                    Error(id!, -32601, $"Unknown tool: {toolName}");
                }
                break;
            }

        default:
            if (!isNotification)
                Error(id!, -32601, $"Method not found: {method}");
            break;
    }
}

Log("stdin closed, shutting down");

// ── Helpers ──────────────────────────────────────────────────────────────────

// Send a successful JSON-RPC 2.0 response.
void Reply(JsonNode id, JsonObject result)
{
    Send(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    });
}

// Send a JSON-RPC 2.0 error response.
void Error(JsonNode id, int code, string message)
{
    Send(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        }
    });
}

// Serialize and write one JSON line to stdout.
void Send(JsonObject obj)
{
    var json = obj.ToJsonString();
    //Log($"→ {json}");
    Console.WriteLine(json);
}

// Write a diagnostic message to stderr (invisible to the MCP client).
void Log(string text) => Console.Error.WriteLine($"[mcp] {text}");
