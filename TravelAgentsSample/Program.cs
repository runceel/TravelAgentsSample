#pragma warning disable SKEXP0050
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using TravelAgentsSample.Agents;
using TravelAgentsSample.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

TokenCredential credential = new DefaultAzureCredential(options: new()
{
    ExcludeVisualStudioCredential = true,
});

builder.AddAzureOpenAIClient("openai",
    configureSettings: settings =>
    {
        settings.Credential = credential;
    });

builder.Services.AddSingleton<MathPlugin>();
builder.Services.AddSingleton(sp =>
    KernelPluginFactory.CreateFromObject(sp.GetRequiredService<MathPlugin>()));
builder.Services.AddSingleton<HiroshimaDialectPlugin>();
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var openAiClient = sp.GetRequiredService<AzureOpenAIClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new AzureOpenAIChatCompletionService(
        "gpt-4o",
        openAiClient,
        loggerFactory: loggerFactory);
});
builder.Services.AddKernel();
builder.Services.AddTransient<AgentFactory>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
