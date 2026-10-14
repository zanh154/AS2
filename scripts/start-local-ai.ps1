$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $workspace '.runtime'
$binary = Join-Path $runtime 'ollama/ollama.exe'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Ollama portable chưa có. Xem docs/local-runtime.md.' }
$running = $false
try { $null = Invoke-RestMethod 'http://localhost:11434/api/version' -TimeoutSec 3; $running = $true } catch {}
if (-not $running) {
    $env:OLLAMA_MODELS = Join-Path $runtime 'models'
    $env:OLLAMA_HOST = '127.0.0.1:11434'
    $env:OLLAMA_NUM_PARALLEL = '1'
    $env:OLLAMA_MAX_LOADED_MODELS = '1'
    $env:OLLAMA_CONTEXT_LENGTH = '4096'
    New-Item -ItemType Directory -Force -Path (Join-Path $runtime 'logs') | Out-Null
    Start-Process -FilePath $binary -ArgumentList 'serve' -WorkingDirectory $runtime -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtime 'logs/ollama-out.log') -RedirectStandardError (Join-Path $runtime 'logs/ollama-error.log') | Out-Null
    for ($attempt=0; $attempt -lt 15; $attempt++) { try { $null = Invoke-RestMethod 'http://localhost:11434/api/version' -TimeoutSec 2; $running = $true; break } catch { Start-Sleep -Seconds 1 } }
}
if (-not $running) { throw 'Không khởi động được AI server.' }
Write-Output 'Ollama đang chạy tại http://localhost:11434. Models lưu trong .runtime/models.'
