$ErrorActionPreference = "Stop"

dotnet build;
if ($LastExitCode -ne 0) { exit $LastExitCode }
cp .\obj\Debug\net46\TCA-VR.dll "$env:TCA_PATH\BepInEx\plugins\";
& "$env:TCA_PATH\Arena.exe" -tca-vr-server-startup "$PSScriptRoot\..\";
