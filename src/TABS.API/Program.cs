using TABS.Causal.Services;
using TABS.Core.Models;
using TABS.Core.Persistence;
using TABS.NLP.Services;
using TABS.OCR.Services;
using TABS.Temporal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpClient<IOCRService, MedGemmaOCRService>();
builder.Services.AddHttpClient<ISimplificationService, MedGemmaSimplificationService>();
builder.Services.AddHttpClient<ICausalInferenceService, BayesianNetworkService>();
builder.Services.AddScoped<ITemporalAnalysisService, DynamicBayesianNetworkService>();

builder.Services.AddSingleton<IRepository<Patient>, InMemoryRepository<Patient>>();
builder.Services.AddSingleton<IRepository<MedicalRecord>, InMemoryRepository<MedicalRecord>>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllLocal", policy =>
    {
        policy.WithOrigins("http://localhost:5001", "http://localhost:5173")
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

app.MapGet("/", () =>
{
                const string html = """
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>3TABS Demo Console</title>
    <style>
        :root {
            --bg: #f6f7f2;
            --card: #ffffff;
            --text: #172121;
            --muted: #4b5a5a;
            --accent: #0f766e;
            --accent-2: #14532d;
            --warn: #b45309;
            --danger: #b91c1c;
            --border: #d6dfd4;
        }
        * { box-sizing: border-box; }
        body {
            margin: 0;
            font-family: ui-sans-serif, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            color: var(--text);
            background: radial-gradient(1200px 700px at 90% -10%, #d7ebe1 0%, transparent 50%), var(--bg);
        }
        .wrap {
            max-width: 1100px;
            margin: 0 auto;
            padding: 28px 18px 56px;
        }
        .hero {
            background: linear-gradient(125deg, #12372a 0%, #1f4d40 55%, #2b6f63 100%);
            color: #f8fffd;
            border-radius: 16px;
            padding: 24px;
            box-shadow: 0 14px 30px rgba(18, 55, 42, 0.25);
        }
        .hero h1 { margin: 0 0 8px; font-size: 1.75rem; }
        .hero p { margin: 0; color: #d9eee7; }
        .grid {
            display: grid;
            grid-template-columns: repeat(12, 1fr);
            gap: 14px;
            margin-top: 16px;
        }
        .card {
            grid-column: span 12;
            background: var(--card);
            border: 1px solid var(--border);
            border-radius: 14px;
            padding: 16px;
            box-shadow: 0 3px 12px rgba(23, 33, 33, 0.06);
        }
        .half { grid-column: span 6; }
        .third { grid-column: span 4; }
        h2, h3 { margin: 0 0 10px; }
        .muted { color: var(--muted); font-size: 0.95rem; }
        .row { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
        input[type="text"], textarea {
            width: 100%;
            border: 1px solid var(--border);
            border-radius: 10px;
            padding: 10px 12px;
            font: inherit;
            color: var(--text);
            background: #fff;
        }
        textarea { min-height: 96px; resize: vertical; }
        button {
            border: 0;
            border-radius: 10px;
            padding: 10px 14px;
            font: inherit;
            background: var(--accent);
            color: #fff;
            cursor: pointer;
            transition: transform .08s ease, opacity .2s ease;
        }
        button:hover { opacity: 0.94; }
        button:active { transform: translateY(1px); }
        .secondary { background: #334155; }
        .good { color: var(--accent-2); font-weight: 600; }
        .warn { color: var(--warn); font-weight: 600; }
        .danger { color: var(--danger); font-weight: 600; }
        .pill {
            display: inline-block;
            border-radius: 999px;
            background: #e6f4ef;
            color: #115e59;
            padding: 4px 10px;
            font-size: 0.82rem;
            margin-right: 6px;
            margin-bottom: 6px;
        }
        pre {
            margin: 0;
            background: #0f172a;
            color: #e5e7eb;
            border-radius: 10px;
            padding: 12px;
            overflow: auto;
            max-height: 320px;
            font-size: 0.84rem;
        }
        table { width: 100%; border-collapse: collapse; font-size: 0.93rem; }
        th, td { padding: 8px; border-bottom: 1px solid var(--border); text-align: left; }
        .links a { color: #0f766e; text-decoration: none; margin-right: 12px; }
        .links a:hover { text-decoration: underline; }
        @media (max-width: 900px) {
            .half, .third { grid-column: span 12; }
        }
    </style>
</head>
<body>
    <div class="wrap">
        <section class="hero">
            <h1>3TABS Live Demo Console</h1>
            <p>Single-page presentation UI for temporal analysis, causal graph suggestions, and medical text simplification.</p>
        </section>

        <section class="grid">
            <article class="card half">
                <h2>Patient Setup</h2>
                <p class="muted">Use any GUID. Keep this constant during your demo for stable flow.</p>
                <div class="row">
                    <input id="patientId" type="text" value="11111111-1111-1111-1111-111111111111" />
                    <button onclick="runAll()">Run Full Analysis</button>
                    <button class="secondary" onclick="loadSuggestions()">Only Suggestions</button>
                </div>
                <p id="status" class="muted" style="margin-top:10px;">Ready</p>
            </article>

            <article class="card half">
                <h2>Medical Text Simplifier</h2>
                <div class="row" style="margin-bottom:10px;">
                    <textarea id="simplifyText">HbA1c remains elevated, suggesting suboptimal glycemic control. Consider tighter lifestyle adherence and medication optimization.</textarea>
                </div>
                <div class="row">
                    <button onclick="simplify()">Simplify For Patient</button>
                </div>
                <p id="simpleOut" class="muted" style="margin-top:10px;"></p>
            </article>

            <article class="card third">
                <h3>Top Suggestion</h3>
                <p id="title" class="muted">No data yet.</p>
                <div id="priority"></div>
            </article>

            <article class="card third">
                <h3>Evidence Chains</h3>
                <div id="evidence" class="muted">No data yet.</div>
            </article>

            <article class="card third">
                <h3>Counterfactuals</h3>
                <div id="counterfactuals" class="muted">No data yet.</div>
            </article>

            <article class="card half">
                <h3>Temporal Trends</h3>
                <div id="trends" class="muted">Run analysis to load trends.</div>
            </article>

            <article class="card half">
                <h3>Causal Graph Snapshot</h3>
                <div id="graph" class="muted">Run analysis to load graph.</div>
            </article>

            <article class="card">
                <h3>Raw API Response</h3>
                <pre id="raw">{}</pre>
            </article>

            <article class="card links">
                <a href="/openapi/v1.json" target="_blank" rel="noreferrer">OpenAPI JSON</a>
                <a href="/api/patients/11111111-1111-1111-1111-111111111111/suggestions" target="_blank" rel="noreferrer">Sample Suggestions API</a>
            </article>
        </section>
    </div>

    <script>
        const byId = (id) => document.getElementById(id);

        const setStatus = (msg, level = "muted") => {
            const el = byId("status");
            el.className = level;
            el.textContent = msg;
        };

        const api = (path) => `${window.location.origin}${path}`;
        const getPatient = () => byId("patientId").value.trim();

        async function loadSuggestions() {
            try {
                setStatus("Loading suggestions...", "warn");
                const patient = getPatient();
                const res = await fetch(api(`/api/patients/${patient}/suggestions`));
                if (!res.ok) throw new Error(`Suggestions failed (${res.status})`);
                const data = await res.json();
                byId("title").textContent = `${data.title}: ${data.description}`;
                byId("priority").innerHTML = `<span class="pill">Priority ${data.priority}</span>`;
                byId("evidence").innerHTML = (data.supportingEvidence || []).map(e => `<div class="pill">${e}</div>`).join("") || "No evidence";
                byId("counterfactuals").innerHTML = (data.counterfactuals || []).map(c => `<p><strong>${c.scenario}</strong><br>${c.outcomeDifference}</p>`).join("") || "No scenarios";
                byId("raw").textContent = JSON.stringify(data, null, 2);
                setStatus("Suggestions loaded", "good");
            } catch (err) {
                setStatus(err.message, "danger");
            }
        }

        async function loadTemporal() {
            const patient = getPatient();
            const res = await fetch(api(`/api/patients/${patient}/temporal-analysis`));
            if (!res.ok) throw new Error(`Temporal analysis failed (${res.status})`);
            const data = await res.json();
            const rows = (data.trends || []).map(t => `
                <tr>
                    <td>${t.variableName}</td>
                    <td>${t.direction}</td>
                    <td>${Number(t.slope).toFixed(3)}</td>
                    <td>${Number(t.volatility).toFixed(3)}</td>
                </tr>
            `).join("");

            byId("trends").innerHTML = rows
                ? `<table><thead><tr><th>Variable</th><th>Direction</th><th>Slope</th><th>Volatility</th></tr></thead><tbody>${rows}</tbody></table>`
                : "No trend data yet. Upload a medical report first to enrich analysis.";
        }

        async function loadGraph() {
            const patient = getPatient();
            const res = await fetch(api(`/api/patients/${patient}/causal-graph?target=health_optimization`));
            if (!res.ok) throw new Error(`Causal graph failed (${res.status})`);
            const data = await res.json();
            const nodes = (data.nodes || []).map(n => `<div class="pill">${n.label} (${Math.round((n.probability || 0) * 100)}%)</div>`).join("");
            const edges = (data.edges || []).map(e => `<li>${e.sourceId} -> ${e.targetId} (${Math.round((e.strength || 0) * 100)}%)</li>`).join("");
            byId("graph").innerHTML = `
                <p class="muted">Target: ${data.targetVariable || "n/a"}</p>
                <div>${nodes}</div>
                <ul>${edges}</ul>
            `;
        }

        async function simplify() {
            try {
                const text = byId("simplifyText").value.trim();
                const res = await fetch(api("/api/patients/simplify"), {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ text, language: "en", readingLevel: 8 })
                });
                if (!res.ok) throw new Error(`Simplify failed (${res.status})`);
                const data = await res.json();
                byId("simpleOut").textContent = data.simplifiedText || "No simplified text returned";
            } catch (err) {
                byId("simpleOut").textContent = err.message;
            }
        }

        async function runAll() {
            try {
                setStatus("Running full analysis...", "warn");
                await Promise.all([loadTemporal(), loadGraph(), loadSuggestions()]);
                setStatus("Demo data loaded", "good");
            } catch (err) {
                setStatus(err.message, "danger");
            }
        }
    </script>
</body>
</html>
""";

        return Results.Content(html, "text/html");
});

app.MapControllers();

app.Run();
