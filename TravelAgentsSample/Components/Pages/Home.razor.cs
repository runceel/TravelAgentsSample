#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Markdig;
using Microsoft.AspNetCore.Components;

namespace TravelAgentsSample.Components.Pages;

public partial class Home
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .Build();
    [Inject]
    public required ILogger<Home> Logger { get; set; }

    private bool _isProcessing;
    private string _inputMessage = "";
    private readonly List<string> _messages = [];
    private async Task TaskAsyncEnumerableExtensions()
    {
        if (_isProcessing) return;

        _isProcessing = true;
        try
        {
            _messages.Clear();
            var agentGroupChat = AgentFactory.CreateTravelAgentGroupChat();
            agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, _inputMessage));
            await foreach (var content in agentGroupChat.InvokeAsync())
            {
                _messages.Add($"""
                {content.AuthorName}:

                {content.Content}
                """);
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
            _messages.Add("エラーが発生しました。");
        }
        finally
        {
            _isProcessing = false;
        }
    }

}
