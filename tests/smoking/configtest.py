import pytest
import requests

BASE_URL = "https://easyhmsapiservices-bgasabd9ddbbdden.centralindia-01.azurewebsites.net"

DOCTOR_LOGIN = {
    "username": "8074906831",     #  Replace with real
    "password": "@Change32"   #  Replace with real
}

@pytest.fixture(scope="session")
def auth_token():
    url = f"{BASE_URL}/auth/user/login"
    r = requests.post(url, json=DOCTOR_LOGIN, timeout=30)
    assert r.status_code == 200, f"Login failed: {r.text}"
    return r.json().get("token")
