from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from tabs_frontend.api_client import TABSApiClient


@dataclass
class AnalysisService:
    api_client: TABSApiClient

    def run_full_analysis(self, patient_id: str) -> dict[str, Any]:
        temporal = self.api_client.temporal_analysis(patient_id)
        graph = self.api_client.causal_graph(patient_id)
        suggestions = self.api_client.suggestions(patient_id)
        return {
            "temporal": temporal,
            "graph": graph,
            "suggestions": suggestions,
        }
