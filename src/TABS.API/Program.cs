using System.Text.Json.Serialization;
using TABS.API.Application;
using TABS.Causal.Services;
using TABS.Core.Models;
using TABS.Core.Persistence;
using TABS.NLP.Services;
using TABS.OCR.Services;
using TABS.Temporal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IOCRService, MedGemmaOCRService>();
builder.Services.AddHttpClient<ISimplificationService, MedGemmaSimplificationService>();
builder.Services.AddHttpClient<ICausalInferenceService, BayesianNetworkService>();
builder.Services.AddScoped<ITemporalAnalysisService, DynamicBayesianNetworkService>();

builder.Services.AddSingleton<IRepository<Patient>, InMemoryRepository<Patient>>();
builder.Services.AddSingleton<IRepository<MedicalRecord>, InMemoryRepository<MedicalRecord>>();

builder.Services.AddScoped<IRecordTypeDetector, RecordTypeDetector>();
builder.Services.AddScoped<IPatientAnalysisOrchestrator, PatientAnalysisOrchestrator>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllLocal", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5001",
                "http://localhost:5173",
                "http://localhost:8501")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAllLocal");
app.UseAuthorization();

app.MapGet("/", () => Results.Json(new
{
    service = "TABS.API",
    status = "running",
    docs = "/openapi/v1.json",
    streamlit = "http://localhost:8501"
}));

app.MapControllers();

app.Run();
