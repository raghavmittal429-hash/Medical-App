from __future__ import annotations

import json

import pandas as pd
import streamlit as st

from tabs_frontend.analysis_service import AnalysisService
from tabs_frontend.api_client import ApiError, TABSApiClient
from tabs_frontend.config import load_config


st.set_page_config(page_title="3TABS Clinical Demo", page_icon="🩺", layout="wide")

config = load_config()
api_client = TABSApiClient(config.api_base_url)
analysis_service = AnalysisService(api_client)


st.title("🩺 3TABS Clinical Intelligence Demo")
st.caption("Modular Streamlit frontend powered by TABS.API")

with st.sidebar:
    st.header("Connection")
    api_base_url = st.text_input("API Base URL", value=config.api_base_url)
    patient_id = st.text_input("Patient ID", value=config.default_patient_id)

    if st.button("Check API Health"):
        try:
            health = TABSApiClient(api_base_url).health()
            st.success("API reachable")
            st.json(health)
        except Exception as ex:
            st.error(str(ex))


client = TABSApiClient(api_base_url)
service = AnalysisService(client)

col1, col2 = st.columns([2, 1])

with col1:
    st.subheader("Upload Medical Document")
    uploaded_file = st.file_uploader("Upload lab report / notes / prescription", type=["pdf", "jpg", "jpeg", "png", "txt"])
    if st.button("Upload and Extract", use_container_width=True, disabled=uploaded_file is None):
        if uploaded_file is not None:
            try:
                with st.spinner("Uploading and extracting structured data..."):
                    upload_result = client.upload_document(
                        patient_id,
                        uploaded_file.name,
                        uploaded_file.getvalue(),
                        uploaded_file.type or "application/octet-stream",
                    )
                st.success("Document processed")
                st.json(upload_result)
            except Exception as ex:
                st.error(str(ex))

with col2:
    st.subheader("Text Simplifier")
    text = st.text_area(
        "Medical text",
        value="HbA1c remains elevated, suggesting suboptimal glycemic control.",
        height=130,
    )
    if st.button("Simplify", use_container_width=True):
        try:
            simplified = client.simplify(text)
            st.success("Simplified")
            st.write(simplified.get("simplifiedText", "No simplified text"))
        except Exception as ex:
            st.error(str(ex))

st.divider()

if st.button("Run Full Analysis", type="primary", use_container_width=True):
    try:
        with st.spinner("Running temporal + causal + suggestion analysis..."):
            results = service.run_full_analysis(patient_id)
        st.session_state["analysis_results"] = results
        st.success("Analysis completed")
    except Exception as ex:
        st.error(str(ex))

results = st.session_state.get("analysis_results")
if results:
    suggestions = results["suggestions"]
    temporal = results["temporal"]
    graph = results["graph"]

    a, b, c = st.columns(3)
    a.metric("Priority", str(suggestions.get("priority", "Unknown")))
    a.caption("Enum is serialized as string in API")
    b.metric("Causal Nodes", len(graph.get("nodes", [])))
    c.metric("Counterfactuals", len(suggestions.get("counterfactuals", [])))

    st.subheader("Top Suggestion")
    st.write(suggestions.get("title", "N/A"))
    st.write(suggestions.get("description", "N/A"))
    st.info(suggestions.get("simplifiedExplanation", "N/A"))

    st.subheader("Evidence Chains")
    for item in suggestions.get("supportingEvidence", []):
        st.markdown(f"- {item}")

    st.subheader("Counterfactual Scenarios")
    for item in suggestions.get("counterfactuals", []):
        st.markdown(f"**{item.get('scenario', 'Scenario')}**")
        st.write(item.get("outcomeDifference", ""))
        st.caption(f"Probability change: {item.get('probabilityChange', 0)}")

    st.subheader("Temporal Trends")
    trends = temporal.get("trends", [])
    if trends:
        trend_rows = [
            {
                "Variable": t.get("variableName"),
                "Direction": t.get("direction"),
                "Slope": t.get("slope"),
                "Volatility": t.get("volatility"),
            }
            for t in trends
        ]
        st.dataframe(pd.DataFrame(trend_rows), use_container_width=True)
    else:
        st.warning("No trends found yet. Upload one or more files to create patient history.")

    st.subheader("Causal Graph Snapshot")
    st.write(f"Target: {graph.get('targetVariable', 'N/A')}")
    graph_nodes = graph.get("nodes", [])
    if graph_nodes:
        node_df = pd.DataFrame(
            [
                {
                    "Node": n.get("label"),
                    "Type": n.get("type"),
                    "Probability": n.get("probability"),
                }
                for n in graph_nodes
            ]
        )
        st.dataframe(node_df, use_container_width=True)

    with st.expander("Raw API Payload"):
        st.code(json.dumps(suggestions, indent=2), language="json")
else:
    st.info("Click 'Run Full Analysis' to fetch results.")
