import requests

BASE_URL = "https://easyhmsapiservices-bgasabd9ddbbdden.centralindia-01.azurewebsites.net"

def test_login_api():
    payload = {
        "username": "8074906831",     #  Replace
        "password": "@Change32"   #  Replace
    }

    response = requests.post(f"{BASE_URL}/auth/user/login", json=payload, timeout=30)

    assert response.status_code == 200
    assert "token" in response.json()