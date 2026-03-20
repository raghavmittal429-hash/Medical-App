# Medical-App

Runnable .NET 10 solution for the 3TABS architecture (OCR + NLP + Temporal + Causal + API).

## Requirements

- .NET SDK 10.0+

## Build

```bash
dotnet build MedicalApp.slnx
```

## Run API

```bash
dotnet run --project src/TABS.API/TABS.API.csproj --urls http://localhost:5088
```

## OpenAPI

- http://localhost:5088/openapi/v1.json

## Quick endpoint test

```bash
curl http://localhost:5088/api/patients/11111111-1111-1111-1111-111111111111/suggestions
```

## Test

```bash
dotnet test MedicalApp.slnx
```

## Notes

- OCR/NLP services call a MedGemma endpoint if available at `http://localhost:8080/v1/chat/completions`.
- If that endpoint is unavailable, the services return safe fallback data so local development still works.
