from __future__ import annotations

from dataclasses import dataclass
from typing import Any

import requests


class ApiError(RuntimeError):
    pass


@dataclass
class TABSApiClient:
    base_url: str
    timeout_seconds: int = 30

    def _url(self, path: str) -> str:
        return f"{self.base_url.rstrip('/')}{path}"

    def _request(self, method: str, path: str, **kwargs: Any) -> Any:
        response = requests.request(
            method,
            self._url(path),
            timeout=self.timeout_seconds,
            **kwargs,
        )
        if response.status_code >= 400:
            raise ApiError(f"{method} {path} failed ({response.status_code}): {response.text}")
        if not response.content:
            return None
        return response.json()

    def health(self) -> dict[str, Any]:
        return self._request("GET", "/")

    def suggestions(self, patient_id: str) -> dict[str, Any]:
        return self._request("GET", f"/api/patients/{patient_id}/suggestions")

    def temporal_analysis(self, patient_id: str) -> dict[str, Any]:
        return self._request("GET", f"/api/patients/{patient_id}/temporal-analysis")

    def causal_graph(self, patient_id: str, target: str = "health_optimization") -> dict[str, Any]:
        return self._request("GET", f"/api/patients/{patient_id}/causal-graph", params={"target": target})

    def simplify(self, text: str, language: str = "en", reading_level: int = 8) -> dict[str, Any]:
        return self._request(
            "POST",
            "/api/patients/simplify",
            json={"text": text, "language": language, "readingLevel": reading_level},
        )

    def upload_document(self, patient_id: str, file_name: str, content: bytes, content_type: str) -> dict[str, Any]:
        files = {"file": (file_name, content, content_type)}
        return self._request("POST", f"/api/patients/{patient_id}/upload", files=files)
