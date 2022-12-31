$ErrorActionPreference = "Stop"

$SERVER_NAME = "TCA_VR-xr_server";

if (test-path "$env:TCA_PATH\$SERVER_NAME") {
    rm "$env:TCA_PATH\$SERVER_NAME" -Recurse;
}

.\env\Scripts\pyinstaller .\xr_server.spec --noconfirm;
cp ".\dist\$SERVER_NAME\" $env:TCA_PATH -Recurse;
