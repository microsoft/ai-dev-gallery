#!/usr/bin/env pwsh
# Foundry Service Diagnostic Script

Write-Host "=== Foundry Service Diagnostics ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check if Foundry CLI is available
Write-Host "1. Checking Foundry CLI availability..." -ForegroundColor Yellow
$foundryPath = Get-Command foundry -ErrorAction SilentlyContinue
if ($foundryPath) {
    Write-Host "   ✓ Foundry CLI found at: $($foundryPath.Source)" -ForegroundColor Green
} else {
    Write-Host "   ✗ Foundry CLI not found in PATH" -ForegroundColor Red
    exit 1
}

# 2. Check Foundry service status
Write-Host ""
Write-Host "2. Checking Foundry service status..." -ForegroundColor Yellow
try {
    $statusOutput = & foundry service status 2>&1
    $exitCode = $LASTEXITCODE
    Write-Host "   Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })
    Write-Host "   Output: $statusOutput" -ForegroundColor Gray
    
    # Extract URL
    if ($statusOutput -match "(https?://[^/]+:\d+)") {
        $serviceUrl = $matches[1]
        Write-Host "   ✓ Service URL: $serviceUrl" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Could not extract service URL" -ForegroundColor Red
        Write-Host ""
        Write-Host "3. Attempting to start Foundry service..." -ForegroundColor Yellow
        $startOutput = & foundry service start 2>&1
        Write-Host "   Output: $startOutput" -ForegroundColor Gray
        
        if ($startOutput -match "(https?://[^/]+:\d+)") {
            $serviceUrl = $matches[1]
            Write-Host "   ✓ Service started at: $serviceUrl" -ForegroundColor Green
        } else {
            Write-Host "   ✗ Failed to start service" -ForegroundColor Red
            exit 1
        }
    }
} catch {
    Write-Host "   ✗ Error: $_" -ForegroundColor Red
    exit 1
}

# 3. Test API endpoints
Write-Host ""
Write-Host "4. Testing Foundry API endpoints..." -ForegroundColor Yellow

# Test /foundry/list
Write-Host "   Testing GET $serviceUrl/foundry/list" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$serviceUrl/foundry/list" -Method Get -UseBasicParsing -TimeoutSec 10
    Write-Host "   ✓ Status: $($response.StatusCode)" -ForegroundColor Green
    $content = $response.Content | ConvertFrom-Json
    Write-Host ($content | ConvertTo-Json -Depth 10) -ForegroundColor Green
    Write-Host "   ✓ Found $($content.Count) models in catalog" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test /openai/models
Write-Host ""
Write-Host "   Testing GET $serviceUrl/openai/models" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$serviceUrl/openai/models" -Method Get -UseBasicParsing -TimeoutSec 10
    Write-Host "   ✓ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   ✓ Content: $($response.Content)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}

# 4. Test the problematic download endpoint
Write-Host ""
Write-Host "5. Testing POST $serviceUrl/openai/download (with sample payload)..." -ForegroundColor Yellow
Write-Host "   Note: This test uses a sample model. To test with a different model, modify the payload below." -ForegroundColor Gray

$testPayload = @{
    model = @{
        name = "openai-whisper-tiny-generic-cpu"
        uri = "azureml://registries/azureml/models/openai-whisper-tiny-generic-cpu/versions/2"
        path = "cpu-fp32"
        providerType = "AzureFoundry"
        promptTemplate = @{
            assistant = $null
            prompt = "<|startoftranscript|> <|en|> <|transcribe|> <|notimestamps|>"
        }
    }
    ignorePipeReport = $true
} | ConvertTo-Json -Depth 10


Write-Host "   Payload:" -ForegroundColor Gray
Write-Host "   $testPayload" -ForegroundColor Gray

try {
    Write-Host ""
    Write-Host "   Sending request..." -ForegroundColor Cyan
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    $response = Invoke-WebRequest -Uri "$serviceUrl/openai/download" `
        -Method Post `
        -ContentType "application/json" `
        -Body $testPayload `
        -UseBasicParsing `
        -TimeoutSec 30
    
    $stopwatch.Stop()
    Write-Host "   ✓ Status: $($response.StatusCode) (took $($stopwatch.ElapsedMilliseconds)ms)" -ForegroundColor Green
    Write-Host "   ✓ Content Length: $($response.Content.Length) bytes" -ForegroundColor Green
    
    if ($response.Content.Length -lt 500) {
        Write-Host "   Response:" -ForegroundColor Gray
        Write-Host "   $($response.Content)" -ForegroundColor Gray
    } else {
        Write-Host "   Response (first 500 chars):" -ForegroundColor Gray
        Write-Host "   $($response.Content.Substring(0, 500))..." -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "   ✓ This machine's Foundry service is working correctly for download requests." -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Error Type: $($_.Exception.GetType().FullName)" -ForegroundColor Red
    
    if ($_.Exception.InnerException) {
        Write-Host "   Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
    
    if ($_.Exception.Response) {
        Write-Host "   HTTP Status Code: $($_.Exception.Response.StatusCode.Value__)" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "   ✗ This machine has the same issue as reported." -ForegroundColor Yellow
    Write-Host "   The Foundry service is failing to handle download requests." -ForegroundColor Yellow
}

# 5. Check for Foundry logs
Write-Host ""
Write-Host "6. Looking for Foundry logs..." -ForegroundColor Yellow
$possibleLogLocations = @(
    "$env:USERPROFILE\.foundry\logs",
    "$env:LOCALAPPDATA\Foundry\logs",
    "$env:TEMP\foundry"
)

$foundLogPath = $null
foreach ($logPath in $possibleLogLocations) {
    if (Test-Path $logPath) {
        Write-Host "   ✓ Found logs at: $logPath" -ForegroundColor Green
        $foundLogPath = $logPath
        $recentLogs = Get-ChildItem -Path $logPath -Filter "*.log" -ErrorAction SilentlyContinue | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 3
        
        if ($recentLogs) {
            Write-Host "   Recent log files:" -ForegroundColor Gray
            $recentLogs | ForEach-Object {
                Write-Host "     - $($_.Name) (Modified: $($_.LastWriteTime))" -ForegroundColor Gray
            }
        }
    }
}

# 6. System information
Write-Host ""
Write-Host "7. System Information..." -ForegroundColor Yellow
try {
    $os = Get-CimInstance Win32_OperatingSystem
    Write-Host "   OS: $($os.Caption) $($os.Version)" -ForegroundColor Gray
    Write-Host "   Architecture: $($os.OSArchitecture)" -ForegroundColor Gray
    
    $mem = Get-CimInstance Win32_ComputerSystem
    $totalMemGB = [math]::Round($mem.TotalPhysicalMemory / 1GB, 2)
    Write-Host "   Total Memory: $totalMemGB GB" -ForegroundColor Gray
    
    $disk = Get-PSDrive C
    $freeGB = [math]::Round($disk.Free / 1GB, 2)
    $usedGB = [math]::Round($disk.Used / 1GB, 2)
    Write-Host "   C: Drive - Used: $usedGB GB, Free: $freeGB GB" -ForegroundColor Gray
    
    $foundryVersion = & foundry --version 2>&1
    Write-Host "   Foundry Version: $foundryVersion" -ForegroundColor Gray
} catch {
    Write-Host "   Could not retrieve all system information" -ForegroundColor Yellow
}
