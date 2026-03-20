from dataclasses import dataclass
import os


@dataclass(frozen=True)
class AppConfig:
    api_base_url: str
    default_patient_id: str


def load_config() -> AppConfig:
    return AppConfig(
        api_base_url=os.getenv("TABS_API_BASE_URL", "http://localhost:5088"),
        default_patient_id=os.getenv("TABS_DEFAULT_PATIENT_ID", "11111111-1111-1111-1111-111111111111"),
    )
