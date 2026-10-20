# MCP implementation validation

Reviewed against the official C# SDK documentation, the installed ModelContextProtocol 2.2.0 API documentation, and the MCP tools specification. The host remains on the official SDK; no custom JSON-RPC implementation was introduced.

| Area | Finding and resulting behavior | Verification |
| --- | --- | --- |
| Transport and discovery | Existing AddMcpServer, stdio transport and WithTools registration match SDK guidance. Server identity is explicitly worklog plus the application version. | SDK client initialization and tools/list assertions |
| Standard output | Existing logging to stderr was correct. Successful serve operation reserves stdout for protocol messages. | End-to-end stdio tests and raw JSON-RPC lifecycle test |
| Tool results | Original POCO returns provided JSON text only. Enable UseStructuredContent to advertise outputSchema and populate structuredContent while retaining text content. | Assert schemas, structured results, and semantic equality with fallback JSON text |
| Annotations | Logging is local, additive, non-destructive, and not unconditionally idempotent because its retry key is optional. Querying is local, read-only and idempotent. | Assert advertised annotations |
| Tool failures | Existing McpException mapping is appropriate for recoverable application validation failures. Add explicit null guards for tool request objects. SDK retains responsibility for protocol errors and argument deserialization. | Invalid status returns isError with a useful message; subsequent calls still work |
| Lifecycle | SDK owns stdio session lifetime. Serve forwards the CLI cancellation token to the host. | Closing client stdin after initialization exits cleanly with code zero and no stray stdout |
| Input guidance | Describe timestamp bounds, status meanings, classification overrides and retry-key semantics in generated input schemas. | Tool discovery through the SDK client |

System.CommandLine 2.0.13 replaces the manual CLI parser. Init and serve have separate option sets; generated help and validation run before command actions. Regression cases cover missing values, unsupported clients, unknown options, foreign-command options, help and version behavior.

The journal schema and the entry/report JSON field shapes remain unchanged. Output schema/structuredContent and tool annotations are deliberate additions to the MCP contract. CLI help/error text now comes from System.CommandLine, and options that never applied to serve are rejected.

This verifies the implementation and local protocol behavior. It does not claim complete conformance to every optional MCP feature or replace the pending live Claude Code verification.

## Sources

- [Official C# SDK: tools, structured content, errors and annotations](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md)
- [Official C# SDK: stdio server setup and stderr logging](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [MCP tools specification](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [System.CommandLine parsing and invocation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-parse-and-invoke)
- [System.CommandLine validation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-customize-parsing-and-validation)
