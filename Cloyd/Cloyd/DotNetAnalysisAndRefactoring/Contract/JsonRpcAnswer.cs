using System.Text.Json;

namespace Cloyd.DotNetAnalysisAndRefactoring.Contract;

/// <summary>
/// The MCP's JSON-RPC answer to a <c>tools/call</c>: the tool's answer, in its <see cref="Result"/>, or an
/// <see cref="Error"/> when the call itself failed.
/// </summary>
sealed record JsonRpcAnswer(ToolResult? Result, JsonElement? Error);
