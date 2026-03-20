$modelDir  = Join-Path $PSScriptRoot "models"
$modelFile = Join-Path $modelDir "ggml-tiny.en.bin"
$url       = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin"

if (Test-Path $modelFile) {
    Write-Host "Model already exists at $modelFile"
    exit 0
}

New-Item -ItemType Directory -Path $modelDir -Force | Out-Null
Write-Host "Downloading ggml-tiny.en.bin (~75 MB)..."
Invoke-WebRequest -Uri $url -OutFile $modelFile
Write-Host "Done. Model saved to $modelFile"
