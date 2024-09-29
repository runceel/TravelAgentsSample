var builder = DistributedApplication.CreateBuilder(args);

var openai = builder.AddConnectionString("openai");
builder.AddProject<Projects.TravelAgentsSample>("travelagentssample")
    .WithReference(openai);

builder.Build().Run();
