# Medical-App

3TABS clinical demo application with:

- Modular .NET 10 backend API
- Streamlit frontend for live demos
- OCR/NLP/Temporal/Causal services with fallback behavior for local runs

## Tech Stack

- Backend: ASP.NET Core (.NET 10)
- Frontend: Streamlit (Python)
- Data store: In-memory repositories (demo mode)
- API docs: OpenAPI (`/openapi/v1.json`)

## Project Structure

- `src/TABS.API` - Web API host, controllers, orchestration layer
- `src/TABS.Core` - Domain models and repository abstractions
- `src/TABS.OCR` - OCR extraction service
- `src/TABS.NLP` - Medical text simplification service
- `src/TABS.Temporal` - Temporal analysis service
- `src/TABS.Causal` - Causal inference and suggestions service
- `frontend-streamlit` - Streamlit UI app
- `tests/TABS.Tests` - xUnit tests

## From Scratch Setup

### 1. Clone and open

```bash
git clone https://github.com/raghavmittal429-hash/Medical-App.git
cd Medical-App
```

### 2. Install .NET SDK (if missing)

```bash
brew install --cask dotnet-sdk
dotnet --version
```

### 3. Create Python virtual environment

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

### 4. Build and test backend

```bash
dotnet build MedicalApp.slnx
dotnet test MedicalApp.slnx
```

## Run The App

Use two terminals.

### Terminal A: Start API

```bash
cd Medical-App
dotnet run --project src/TABS.API/TABS.API.csproj --urls http://localhost:5088
```

### Terminal B: Start Streamlit

```bash
cd Medical-App
source .venv/bin/activate
streamlit run frontend-streamlit/app.py --server.port 8501
```

## URLs

- Streamlit UI: http://localhost:8501
- API health metadata: http://localhost:5088/
- OpenAPI: http://localhost:5088/openapi/v1.json
- Suggestions endpoint: http://localhost:5088/api/patients/11111111-1111-1111-1111-111111111111/suggestions

## End-to-End Quick Check

```bash
# Suggestions
curl http://localhost:5088/api/patients/11111111-1111-1111-1111-111111111111/suggestions

# Simplify text
curl -X POST http://localhost:5088/api/patients/simplify \
	-H "Content-Type: application/json" \
	-d '{"text":"HbA1c elevated","language":"en","readingLevel":8}'

# Upload a sample file
echo "Sample report" > /tmp/sample-report.txt
curl -X POST http://localhost:5088/api/patients/11111111-1111-1111-1111-111111111111/upload \
	-F "file=@/tmp/sample-report.txt;type=text/plain"
```

## Notes

- If MedGemma is unavailable at `http://localhost:8080/v1/chat/completions`, the app uses safe fallback responses so demos still run.
- Data is in-memory; restarting API resets patient history.

## Modular Design (SOLID-Oriented)

- Controller delegates use-cases to `IPatientAnalysisOrchestrator`.
- Record type detection is isolated in `IRecordTypeDetector`.
- Domain services are interface-first:
	- `IOCRService`
	- `ISimplificationService`
	- `ITemporalAnalysisService`
	- `ICausalInferenceService`
- Streamlit frontend is modularized into:
	- `frontend-streamlit/tabs_frontend/config.py`
	- `frontend-streamlit/tabs_frontend/api_client.py`
	- `frontend-streamlit/tabs_frontend/analysis_service.py`
