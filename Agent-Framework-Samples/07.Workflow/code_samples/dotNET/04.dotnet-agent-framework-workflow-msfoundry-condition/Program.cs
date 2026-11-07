using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ClientModel;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.Workflows;
using DotNetEnv;

// Load environment variables
Env.Load("../../../../.env");

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? Environment.GetEnvironmentVariable("GITHUB_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL")
    ?? Environment.GetEnvironmentVariable("GITHUB_MODEL_ID")
    ?? "deepseek-chat";
var apiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY")
    ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("FOUNDRY_API_KEY or GITHUB_TOKEN is not set.");

bool forceShortDraft = args.Contains("--short", StringComparer.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("FORCE_SHORT_DRAFT"), "true", StringComparison.OrdinalIgnoreCase);

// Agent instructions
string EvangelistInstructions = forceShortDraft
    ? @"
You are a technology evangelist creating a VERY SHORT summary note (strictly under 40 words total).
1. Write a brief 2-sentence summary of AI Agents in strictly fewer than 40 words so that it fails the 200-word minimum check.
2. Output ONLY valid JSON (no markdown code fences) with field 'draft_content' (string)."
    : @"
You are a technology evangelist create a first draft for a technical tutorials.
1. Each knowledge point in the outline must include a link. Follow the link to access the content related to the knowledge point in the outline. Expand on that content.
2. Each knowledge point must be explained in detail.
3. Rewrite the content according to the entry requirements, including the title, outline, and corresponding content. It is not necessary to follow the outline in full order.
4. The content must be more than 200 words (write at least 350 words of detailed technical tutorial content in Markdown).
5. return result as pure valid JSON (without markdown code fences) with field 'draft_content' (string).";

const string ContentReviewerInstructions = @"
You are a content reviewer and need to check whether the tutorial's draft content meets the following requirements:

1. If the draft content has less than 200 words, set 'review_result' to 'No' and 'reason' to 'Content is too short'. If the draft content has more than 200 words, set 'review_result' to 'Yes' and 'reason' to 'The content is good'.
2. set 'draft_content' to the original draft content.
3. return result as pure valid JSON (without ```json fences) with fields 'review_result' ('Yes' or 'No') and 'reason' (string) and 'draft_content' (string).";

const string PublisherInstructions = @"
You are the content publisher, run the SaveMarkdownFile tool to save the tutorial's draft content as a Markdown file. Saved file's name is marked with current date and time, such as yearmonthdayhourminsec. Note that if it is 1-9, you need to add 0, such as 20240101123045.md.
";

string OUTLINE_Content = @"
# Introduce AI Agent

## What's AI Agent

https://github.com/microsoft/ai-agents-for-beginners/tree/main/01-intro-to-ai-agents

***Note*** Don's create any sample code 

## Introduce Azure AI Foundry Agent Service 

https://learn.microsoft.com/en-us/azure/ai-foundry/agents/overview

***Note*** Don's create any sample code 

## Microsoft Agent Framework 

https://github.com/microsoft/agent-framework/tree/main/docs/docs-templates

***Note*** Don's create any sample code 
";

// Configure OpenAI-compatible client for the AI Project endpoint (DeepSeek / OpenAI-compatible)
var openAIOptions = new OpenAIClientOptions
{
    Endpoint = new Uri(endpoint)
};
var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), openAIOptions);
IChatClient baseChatClient = new ChatClientBuilder(openAIClient.GetChatClient(deploymentName).AsIChatClient())
    .UseFunctionInvocation()
    .Build();

[Description("Saves the approved tutorial draft content into a timestamped Markdown (.md) file on disk.")]
static string SaveMarkdownFile(
    [Description("The markdown content of the approved tutorial draft.")] string content,
    [Description("Optional filename in format yyyyMMddHHmmss.md")] string? fileName = null)
{
    string resolvedName = !string.IsNullOrWhiteSpace(fileName) && fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        ? Path.GetFileName(fileName)
        : $"{DateTime.Now:yyyyMMddHHmmss}.md";
    File.WriteAllText(resolvedName, content, Encoding.UTF8);
    Console.WriteLine($"[Tool:SaveMarkdownFile] Saved Markdown file: {Path.GetFullPath(resolvedName)} ({content.Length} chars)");
    return $"Saved successfully to {resolvedName}";
}

// Create evangelist agent
AIAgent evangelistagent = baseChatClient.AsAIAgent(
    instructions: EvangelistInstructions,
    name: "dotNETEvangelist");

// Create content reviewer agent
AIAgent contentRevieweragent = baseChatClient.AsAIAgent(
    instructions: ContentReviewerInstructions,
    name: "dotNETContentReviewer");

// Create publisher agent with Markdown file saving tool
AIAgent publisheragent = baseChatClient.AsAIAgent(
    instructions: PublisherInstructions,
    name: "dotNETPublisher",
    tools: [AIFunctionFactory.Create(SaveMarkdownFile)]);

// Create executors
var draftExecutor = new DraftExecutor(evangelistagent);
var contentReviewerExecutor = new ContentReviewExecutor(contentRevieweragent);
var publishExecutor = new PublishExecutor(publisheragent);
var sendReviewerExecutor = new SendReviewExecutor();

// Build workflow with conditional logic
var workflow = new WorkflowBuilder(draftExecutor)
    .AddEdge(draftExecutor, contentReviewerExecutor)
    .AddEdge(contentReviewerExecutor, publishExecutor, condition: GetCondition(expectedResult: "Yes"))
    .AddEdge(contentReviewerExecutor, sendReviewerExecutor, condition: GetCondition(expectedResult: "No"))
    .WithOutputFrom(publishExecutor, sendReviewerExecutor)
    .Build();

// Prepare prompt
string prompt = forceShortDraft
    ? "Write a very short draft (strictly under 35 words total) summarizing AI Agents so we can test the rejection branch of the reviewer."
    : @"You need to write a draft based on the following outline and the content provided in the link corresponding to the outline. 
After draft create , the reviewer check it , if it meets the requirements, it will be submitted to the publisher and save it as a Markdown file, 
otherwise need to rewrite draft until it meets the requirements.
The provided outline content and related links is as follows:" + OUTLINE_Content;

Console.WriteLine($"Starting workflow... (Mode: {(forceShortDraft ? "SHORT DRAFT -> Expected 'No' / Rejected Branch" : "FULL TUTORIAL -> Expected 'Yes' / Approved Branch")})");

// Execute workflow
var chat = new ChatMessage(ChatRole.User, prompt);
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, chat);

// Process workflow events
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        Console.WriteLine($"\n=== WorkflowOutputEvent ===\n{outputEvent.Data ?? outputEvent}");
    }
}

// Mermaid
Console.WriteLine("\nMermaid string: \n=======");
var mermaid = workflow.ToMermaidString();
Console.WriteLine(mermaid);
Console.WriteLine("=======");

// DOT - Save to file instead of stdout to avoid pipe issues
var dotString = workflow.ToDotString();
var dotFilePath = "workflow.dot";
File.WriteAllText(dotFilePath, dotString);
Console.WriteLine($"\nDOT graph saved to: {dotFilePath}");
Console.WriteLine("To generate image: dot -Tsvg workflow.dot -o workflow.svg");
Console.WriteLine("                   dot -Tpng workflow.dot -o workflow.png");

// Helper function for conditional routing
static Func<object?, bool> GetCondition(string expectedResult) =>
    reviewResult => reviewResult is ReviewResult review && review.Result == expectedResult;

public static class JsonHelper
{
    public static string CleanJson(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }
        }
        return trimmed.Trim();
    }
}

// Data Models
public class ContentResult
{
    [JsonPropertyName("draft_content")]
    public string DraftContent { get; set; } = string.Empty;
}

public class ReviewResult
{
    [JsonPropertyName("review_result")]
    public string Result { get; set; } = string.Empty;
    
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
    
    [JsonPropertyName("draft_content")]
    public string DraftContent { get; set; } = string.Empty;
}

// Executor: Draft Creation
[SendsMessage(typeof(ContentResult))]
public partial class DraftExecutor : Executor
{
    private readonly AIAgent _evangelistAgent;

    public DraftExecutor(AIAgent evangelistAgent) : base("DraftExecutor")
    {
        this._evangelistAgent = evangelistAgent;
    }

    [MessageHandler]
    public async ValueTask<ContentResult> HandleAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"DraftExecutor .......loading \n" + message.Text);
        
        var response = await this._evangelistAgent.RunAsync(message, cancellationToken: cancellationToken);
        Console.WriteLine($"DraftExecutor response: {response.Text}");

        string draftText = response.Text ?? "";
        try
        {
            var parsed = JsonSerializer.Deserialize<ContentResult>(JsonHelper.CleanJson(draftText));
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.DraftContent))
            {
                draftText = parsed.DraftContent;
            }
        }
        catch
        {
            // Keep raw response.Text if not wrapped in JSON
        }

        ContentResult contentResult = new ContentResult { DraftContent = draftText };
        int wordCount = draftText.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        Console.WriteLine($"DraftExecutor generated content ({wordCount} words): {contentResult.DraftContent}");

        return contentResult;
    }
}

// Executor: Content Review
[SendsMessage(typeof(ReviewResult))]
public partial class ContentReviewExecutor : Executor
{
    private readonly AIAgent _contentReviewerAgent;

    public ContentReviewExecutor(AIAgent contentReviewerAgent) : base("ContentReviewExecutor")
    {
        this._contentReviewerAgent = contentReviewerAgent;
    }

    [MessageHandler]
    public async ValueTask<ReviewResult> HandleAsync(ContentResult content, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"ContentReviewExecutor .......loading");
        var response = await this._contentReviewerAgent.RunAsync(content.DraftContent, cancellationToken: cancellationToken);
        var reviewResult = JsonSerializer.Deserialize<ReviewResult>(JsonHelper.CleanJson(response.Text)) 
            ?? throw new InvalidOperationException("Failed to deserialize review result");
        Console.WriteLine($"ContentReviewExecutor review result: {reviewResult.Result}, reason: {reviewResult.Reason}");

        return reviewResult;
    }
}

// Executor: Publishing
[YieldsOutput(typeof(string))]
public partial class PublishExecutor : Executor
{
    private readonly AIAgent _publishAgent;

    public PublishExecutor(AIAgent publishAgent) : base("PublishExecutor")
    {
        this._publishAgent = publishAgent;
    }

    [MessageHandler]
    public async ValueTask HandleAsync(ReviewResult review, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"PublishExecutor .......loading");
        var response = await this._publishAgent.RunAsync(review.DraftContent, cancellationToken: cancellationToken);
        Console.WriteLine($"Response from PublishExecutor: {response.Text}");
        await context.YieldOutputAsync($"Publishing result: {response.Text}");
    }
}

// Executor: Send Review Notification
[YieldsOutput(typeof(string))]
public partial class SendReviewExecutor : Executor
{
    public SendReviewExecutor() : base("SendReviewExecutor")
    {
    }

    [MessageHandler]
    public async ValueTask HandleAsync(ReviewResult message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        await context.YieldOutputAsync($"Draft content needs revision: {message.Reason}");
}
