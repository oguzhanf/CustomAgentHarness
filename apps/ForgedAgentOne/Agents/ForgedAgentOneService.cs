namespace YourCustomAgentHarness.ForgedAgentOne;

using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using YourCustomAgentHarness.Shared.Manifest;
using YourCustomAgentHarness.Shared.Telemetry;

public sealed class ForgedAgentOneService
{
    private readonly Kernel _kernel;
    private readonly AgentBlueprint _blueprint;
    private readonly IHttpClientFactory _httpFactory;

    public ForgedAgentOneService(Kernel kernel, AgentBlueprint blueprint, IHttpClientFactory httpFactory)
    {
        _kernel = kernel;
        _blueprint = blueprint;
        _httpFactory = httpFactory;

        // OBO-aware Graph plugin — uses the user's access token if supplied
        _kernel.Plugins.AddFromObject(new GraphPlugin(httpFactory), "graph");
    }

    public async Task<string> ChatAsync(ChatRequest req, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var hasObo = !string.IsNullOrEmpty(req.UserAccessToken);
        ActivityStream.Instance.Emit(ActivityEvent.Create(
            "ForgedAgentOne", "model", "chat.invoke", "ok",
            new Dictionary<string, object?>
            {
                ["model"] = _blueprint.Model.DeploymentName,
                ["user"] = req.UserUpn ?? "anonymous",
                ["hasObo"] = hasObo
            }));

        var signInState = hasObo
            ? $"The user IS SIGNED IN as {req.UserUpn ?? "(unknown UPN)"} and a live Microsoft Graph access token is attached to this request."
            : "The user is NOT signed in — no Microsoft Graph token is attached to this request.";

        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine("You are ForgedAgentOne, AgenticBank's relationship-manager copilot.");
        systemPrompt.AppendLine($"You are running inside the {nameof(YourCustomAgentHarness)} custom agent harness.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("=== SESSION STATE ===");
        systemPrompt.AppendLine(signInState);
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("=== TOOL-USE RULES (FOLLOW STRICTLY) ===");
        systemPrompt.AppendLine("- When the user asks about MAIL, EMAILS, INBOX, or messages → ALWAYS call graph.list_recent_mail.");
        systemPrompt.AppendLine("- When the user asks about CALENDAR, MEETINGS, or SCHEDULE → ALWAYS call graph.list_today_meetings.");
        systemPrompt.AppendLine("- When the user asks to FIND a document, policy, contract, or SharePoint file → ALWAYS call graph.search_sharepoint.");
        systemPrompt.AppendLine("- NEVER refuse to call a tool because you think the user might not be signed in.");
        systemPrompt.AppendLine("  The tool itself decides whether to call Graph (live OBO) or to return canned demo data.");
        systemPrompt.AppendLine("  Your job is to invoke the tool and then summarise the result for the banker.");
        systemPrompt.AppendLine("- If a tool's reply begins with '(Graph returned …' or '(Graph call failed' or contains the word 'canned', you may add a brief footer noting that the data is demo / fallback and offer to retry once signed in.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("=== STYLE ===");
        systemPrompt.AppendLine("Never invent account numbers, IBANs, card numbers, or other sensitive identifiers.");
        systemPrompt.AppendLine("Be concise, professional, banking-appropriate. Use bullet points for lists.");

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(systemPrompt.ToString());
        history.AddUserMessage(req.Message ?? "");

        // Inject OBO token + UPN into kernel context so the plugin can read them
        _kernel.Data["UserAccessToken"] = req.UserAccessToken ?? "";
        _kernel.Data["UserUpn"] = req.UserUpn ?? "";

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = _blueprint.Model.Temperature,
            MaxTokens = _blueprint.Model.MaxTokens,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        try
        {
            var result = await chat.GetChatMessageContentAsync(history, settings, _kernel, ct);
            sw.Stop();
            ActivityStream.Instance.Emit(ActivityEvent.Create(
                "ForgedAgentOne", "model", "chat.complete", "ok", durationMs: sw.Elapsed.TotalMilliseconds));
            return result.Content ?? "(empty response)";
        }
        catch (Exception ex)
        {
            sw.Stop();
            ActivityStream.Instance.Emit(ActivityEvent.Create(
                "ForgedAgentOne", "model", "chat.complete", "error",
                new Dictionary<string, object?> { ["error"] = ex.Message },
                sw.Elapsed.TotalMilliseconds));
            return $"I'm unable to reach the Azure OpenAI deployment right now ({ex.GetType().Name}). " +
                   $"In a real workshop run we would retry through the harness's resilience policy.";
        }
    }
}
