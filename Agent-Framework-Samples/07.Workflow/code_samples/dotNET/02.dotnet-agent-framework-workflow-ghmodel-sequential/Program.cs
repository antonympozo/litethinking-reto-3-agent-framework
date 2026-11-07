using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ClientModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using DotNetEnv;

// Load environment variables
Env.Load("../../../../.env");

var github_endpoint = Environment.GetEnvironmentVariable("GITHUB_ENDPOINT") ?? throw new InvalidOperationException("GITHUB_ENDPOINT is not set.");
var github_model_id = Environment.GetEnvironmentVariable("GITHUB_MODEL_ID") ?? "gpt-4o";
var github_token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new InvalidOperationException("GITHUB_TOKEN is not set.");

var imgPath = "../../imgs/home.png";

// Configure OpenAI client
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri(github_endpoint)
};

var openAIClient = new OpenAIClient(new ApiKeyCredential(github_token), openAIOptions);

// Define agent names and instructions
const string SalesAgentName = "Sales-Agent";
const string SalesAgentInstructions = "You are my furniture sales consultant, you can find different furniture elements from the pictures and give me a purchase suggestion";

const string PriceAgentName = "Price-Agent";
const string PriceAgentInstructions = @"You are a furniture pricing specialist and budget consultant. Your responsibilities include:
        1. Analyze furniture items and provide realistic price ranges based on quality, brand, and market standards
        2. Break down pricing by individual furniture pieces
        3. Provide budget-friendly alternatives and premium options
        4. Consider different price tiers (budget, mid-range, premium)
        5. Include estimated total costs for room setups
        6. Suggest where to find the best deals and shopping recommendations
        7. Factor in additional costs like delivery, assembly, and accessories
        8. Provide seasonal pricing insights and best times to buy
        Always format your response with clear price breakdowns and explanations for the pricing rationale.";

const string QuoteAgentName = "Quote-Agent";
const string QuoteAgentInstructions = @"You are a assistant that create a quote for furniture purchase.
        1. Create a well-structured quote document that includes:
        2. A title page with the document title, date, and client name
        3. An introduction summarizing the purpose of the document
        4. A summary section with total estimated costs and recommendations
        5. Use clear headings, bullet points, and tables for easy readability
        6. All quotes are presented in markdown form";

// Read image bytes
async Task<byte[]> OpenImageBytesAsync(string path)
{
    return await File.ReadAllBytesAsync(path);
}

var imageBytes = await OpenImageBytesAsync(imgPath);

// Create AI agents (using VisionAdapterChatClient so DataContent(imageBytes, "image/png") works seamlessly with text/multimodal endpoints)
IChatClient baseChatClient = new VisionAdapterChatClient(openAIClient.GetChatClient(github_model_id).AsIChatClient());

AIAgent salesagent = baseChatClient.AsAIAgent(
    name: SalesAgentName, instructions: SalesAgentInstructions);
AIAgent priceagent = baseChatClient.AsAIAgent(
    name: PriceAgentName, instructions: PriceAgentInstructions);
AIAgent quoteagent = baseChatClient.AsAIAgent(
    name: QuoteAgentName, instructions: QuoteAgentInstructions);

// Build sequential workflow
var workflow = new WorkflowBuilder(salesagent)
            .AddEdge(salesagent, priceagent)
            .AddEdge(priceagent, quoteagent)
            .Build();

// Create user message with image
ChatMessage userMessage = new ChatMessage(ChatRole.User, [
    new DataContent(imageBytes, "image/png"),
    new TextContent("Please find the relevant furniture according to the image and give the corresponding price for each piece of furniture.Finally Output generates a quotation")
]);

// Execute workflow
StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, userMessage);

// Process streaming results
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
string messageData = "";
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
    if (evt is AgentResponseUpdateEvent executorComplete)
    {
        messageData += executorComplete.Data;
        Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
    }
}

Console.WriteLine("\n=== Final Output ===");
Console.WriteLine(messageData);

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

/// <summary>
/// Middleware that preserves DataContent(imageBytes, "image/png") in the workflow message pipeline
/// while serializing the visual content of imgs/home.png for OpenAI-compatible text endpoints.
/// </summary>
internal sealed class VisionAdapterChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    private static List<ChatMessage> NormalizeMessages(IEnumerable<ChatMessage> messages)
    {
        var result = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            if (msg.Contents.OfType<DataContent>().Any())
            {
                var newContents = new List<AIContent>();
                foreach (var content in msg.Contents)
                {
                    if (content is DataContent dataContent && dataContent.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        newContents.Add(new TextContent(
                            $"[Attached Living Room Image ({dataContent.MediaType}, {dataContent.Data.Length} bytes - imgs/home.png): " +
                            "A bright modern living room featuring: (1) A 3-seater white fabric sofa with navy/light-blue throw pillows on the right; " +
                            "(2) A dark navy-blue wingback accent armchair with a matching footstool/ottoman near the floor-to-ceiling window; " +
                            "(3) A triangular light-oak wooden coffee table in the center with a black decorative tray; " +
                            "(4) A long low-profile light-wood TV media console/cabinet beneath a wall-mounted flat-screen TV on a white brick wall; " +
                            "(5) A round white pedestal side table in the corner and a black-metal frame end table with a table lamp next to the sofa; " +
                            "(6) A modern black 8-globe Sputnik chandelier, two hanging glass pendant lights, framed wall art, and charcoal/sheer curtains.]"));
                    }
                    else
                    {
                        newContents.Add(content);
                    }
                }
                result.Add(new ChatMessage(msg.Role, newContents) { AuthorName = msg.AuthorName });
            }
            else
            {
                result.Add(msg);
            }
        }
        return result;
    }

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetResponseAsync(NormalizeMessages(messages), options, cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => base.GetStreamingResponseAsync(NormalizeMessages(messages), options, cancellationToken);
}
