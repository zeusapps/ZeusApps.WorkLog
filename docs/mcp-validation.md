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
| Output guidance | Describe response models and their fields, including work versus save timestamps, replay outcomes, classification snapshots, ticket scopes and duplicate flat/grouped views. | Assert descriptions in tools/list output schemas, including nested entry, ticket and daily-group schemas |

System.CommandLine 2.0.13 replaces the manual CLI parser. Init and serve have separate option sets; generated help and validation run before command actions. Regression cases cover missing values, unsupported clients, unknown options, foreign-command options, help and version behavior.

The journal schema remains unchanged. Report schema version 1 deliberately adds compact pagination and selectable views. CLI help/error text comes from System.CommandLine, and options that never applied to serve are rejected.

## Audit corrections

The independent 2026-10-20 audit found that nullable required output properties were omitted. Earlier tests checked schema presence, descriptions and text equivalence but did not validate complete results against the schemas. Dedicated MCP serialization now emits explicit nulls. Tests use JsonSchema.Net to evaluate actual stdio responses against tools/list schemas, including empty results and every report view; a deliberately incomplete response verifies that validation fails.

Regression tests cover effective-repository ticket inference, order-independent ticket-set grouping, trimmed ticket fields and filters, indexed errors for null tickets, numeric Azure DevOps IDs, and missing period fields. Unknown input fields are rejected rather than silently ignored. Reports use SQL filtering and snapshot keyset pagination, with a 64 KiB structured-payload budget, default 25 observations, selectable views and optional summary exclusion. Tests traverse timestamp ties, concurrent backdated writes, Unicode summaries and every projection without losing entries.

An SDK call-tool request filter checks unknown field names before argument deserialization, using the same serializer metadata as the tool bindings. Errors expose the offending path, such as entry.sumary, entry.tickets[0].urll or period.pagesizee, without exposing internal exception details. Protocol tests verify these errors, absence of partial writes and successful queries after rejection. Other deserialization and domain validation remain with the SDK and journal respectively.

Context discovery, auditable corrections, search tools and schema-level status/provider enums remain potential follow-ups; they are not implemented by these fixes.

This verifies the implementation and local protocol behavior. It does not claim complete conformance to every optional MCP feature or replace the pending live Claude Code verification.

## Sources

- [Official C# SDK: tools, structured content, errors and annotations](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md)
- [Official C# SDK: request filters](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/filters.md)
- [Official C# SDK: stdio server setup and stderr logging](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [MCP tools specification](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [System.CommandLine parsing and invocation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-parse-and-invoke)
- [System.CommandLine validation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-customize-parsing-and-validation)
