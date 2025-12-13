# FoundryLocal API Debug Script
# This script tests the FoundryLocal chat completions endpoint

param(
    [string]$BaseUrl = "http://127.0.0.1:55679",
    [string]$ModelId = "qwen2.5-0.5b-instruct-openvino-npu:3",
    [string]$Prompt = "Hello, how are you?",
    [switch]$Stream = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== FoundryLocal API Debug Script ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray
Write-Host "Model ID: $ModelId" -ForegroundColor Gray
Write-Host "Streaming: $Stream" -ForegroundColor Gray
Write-Host ""

# Test 1: Check if the service is running
Write-Host "[Test 1] Checking if service is running..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "$BaseUrl/v1/models" -Method GET -TimeoutSec 5 -UseBasicParsing
    Write-Host "✓ Service is running" -ForegroundColor Green
    Write-Host "Status Code: $($healthResponse.StatusCode)" -ForegroundColor Gray
    Write-Host "Response:" -ForegroundColor Gray
    Write-Host $healthResponse.Content -ForegroundColor DarkGray
    Write-Host ""
} catch {
    Write-Host "✗ Service is NOT running or not accessible" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Non-streaming chat completion
Write-Host "[Test 2] Testing NON-STREAMING chat completion..." -ForegroundColor Yellow
$requestBody = @{
    model = $ModelId
    messages = @(
        @{
            role = "system"
            content = "You are a helpful assistant."
        },
        @{
            role = "user"
            content = $Prompt
        }
    )
    stream = $false
    temperature = 0.7
    max_tokens = 100
} | ConvertTo-Json -Depth 10

Write-Host "Request Body:" -ForegroundColor Gray
Write-Host $requestBody -ForegroundColor DarkGray
Write-Host ""

try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Invoke-WebRequest `
        -Uri "$BaseUrl/v1/chat/completions" `
        -Method POST `
        -ContentType "application/json" `
        -Body $requestBody `
        -TimeoutSec 30 `
        -UseBasicParsing
    $stopwatch.Stop()
    
    Write-Host "✓ Non-streaming request successful" -ForegroundColor Green
    Write-Host "Status Code: $($response.StatusCode)" -ForegroundColor Gray
    Write-Host "Time taken: $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Gray
    Write-Host "Response Headers:" -ForegroundColor Gray
    foreach ($header in $response.Headers.GetEnumerator()) {
        Write-Host "  $($header.Key): $($header.Value)" -ForegroundColor DarkGray
    }
    Write-Host ""
    Write-Host "Response Body:" -ForegroundColor Gray
    $jsonResponse = $response.Content | ConvertFrom-Json
    Write-Host ($jsonResponse | ConvertTo-Json -Depth 10) -ForegroundColor White
    Write-Host ""
    
    if ($jsonResponse.choices -and $jsonResponse.choices[0].message.content) {
        Write-Host "Generated Text:" -ForegroundColor Cyan
        Write-Host $jsonResponse.choices[0].message.content -ForegroundColor White
        Write-Host ""
    }
} catch {
    Write-Host "✗ Non-streaming request failed" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
}

# Test 3: Streaming chat completion (if requested)
if ($Stream) {
    Write-Host "[Test 3] Testing STREAMING chat completion..." -ForegroundColor Yellow
    
    $streamRequestBody = @{
        model = $ModelId
        messages = @(
            @{
                role = "system"
                content = "You are a helpful assistant."
            },
            @{
                role = "user"
                content = $Prompt
            }
        )
        stream = $true
        stream_options = @{
            include_usage = $true
        }
        temperature = 0.7
        max_tokens = 100
    } | ConvertTo-Json -Depth 10
    
    Write-Host "Request Body:" -ForegroundColor Gray
    Write-Host $streamRequestBody -ForegroundColor DarkGray
    Write-Host ""
    
    try {
        # Create HTTP client for streaming
        $httpClient = New-Object System.Net.Http.HttpClient
        $httpClient.Timeout = [TimeSpan]::FromSeconds(60)
        
        $content = New-Object System.Net.Http.StringContent($streamRequestBody, [System.Text.Encoding]::UTF8, "application/json")
        
        Write-Host "Sending streaming request..." -ForegroundColor Gray
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = $httpClient.PostAsync("$BaseUrl/v1/chat/completions", $content).Result
        
        Write-Host "Response Status: $($response.StatusCode)" -ForegroundColor Gray
        Write-Host "Response Headers:" -ForegroundColor Gray
        foreach ($header in $response.Headers) {
            Write-Host "  $($header.Key): $($header.Value -join ', ')" -ForegroundColor DarkGray
        }
        foreach ($header in $response.Content.Headers) {
            Write-Host "  $($header.Key): $($header.Value -join ', ')" -ForegroundColor DarkGray
        }
        Write-Host ""
        
        if ($response.IsSuccessStatusCode) {
            Write-Host "✓ Streaming connection established" -ForegroundColor Green
            Write-Host "Reading stream..." -ForegroundColor Gray
            Write-Host ""
            
            $stream = $response.Content.ReadAsStreamAsync().Result
            $reader = New-Object System.IO.StreamReader($stream)
            
            $chunkCount = 0
            $totalBytes = 0
            $firstChunkTime = $null
            
            Write-Host "--- Stream Output ---" -ForegroundColor Cyan
            
            try {
                while (-not $reader.EndOfStream) {
                    $line = $reader.ReadLine()
                    $totalBytes += $line.Length + 2  # +2 for CRLF
                    
                    if ($chunkCount -eq 0 -and $line) {
                        $firstChunkTime = $stopwatch.Elapsed
                        Write-Host "First chunk received after $($firstChunkTime.TotalSeconds) seconds" -ForegroundColor Green
                    }
                    
                    if ($Verbose -or $line.StartsWith("data:")) {
                        Write-Host $line -ForegroundColor White
                        
                        # Parse SSE data
                        if ($line.StartsWith("data: ") -and $line -ne "data: [DONE]") {
                            try {
                                $jsonData = $line.Substring(6) | ConvertFrom-Json
                                if ($jsonData.choices -and $jsonData.choices[0].delta.content) {
                                    Write-Host $jsonData.choices[0].delta.content -NoNewline -ForegroundColor Cyan
                                }
                            } catch {
                                # Ignore JSON parse errors for non-JSON lines
                            }
                        }
                    }
                    
                    $chunkCount++
                }
                
                $stopwatch.Stop()
                Write-Host ""
                Write-Host "--- End of Stream ---" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "✓ Stream completed successfully" -ForegroundColor Green
                Write-Host "Total chunks: $chunkCount" -ForegroundColor Gray
                Write-Host "Total bytes: $totalBytes" -ForegroundColor Gray
                Write-Host "Total time: $($stopwatch.Elapsed.TotalSeconds) seconds" -ForegroundColor Gray
                if ($firstChunkTime) {
                    Write-Host "Time to first chunk: $($firstChunkTime.TotalSeconds) seconds" -ForegroundColor Gray
                }
            } catch {
                $stopwatch.Stop()
                Write-Host ""
                Write-Host "✗ Error while reading stream" -ForegroundColor Red
                Write-Host "Error: $_" -ForegroundColor Red
                Write-Host "Chunks received before error: $chunkCount" -ForegroundColor Yellow
                Write-Host "Bytes received before error: $totalBytes" -ForegroundColor Yellow
                Write-Host "Time elapsed: $($stopwatch.Elapsed.TotalSeconds) seconds" -ForegroundColor Yellow
            } finally {
                $reader.Close()
                $stream.Close()
            }
        } else {
            Write-Host "✗ Streaming request failed with status $($response.StatusCode)" -ForegroundColor Red
            $errorContent = $response.Content.ReadAsStringAsync().Result
            Write-Host "Error response: $errorContent" -ForegroundColor Red
        }
        
        $httpClient.Dispose()
    } catch {
        Write-Host "✗ Streaming request failed" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
        Write-Host "Exception Type: $($_.Exception.GetType().FullName)" -ForegroundColor Red
        if ($_.Exception.InnerException) {
            Write-Host "Inner Exception: $($_.Exception.InnerException.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "=== Debug Script Completed ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Yellow
Write-Host "  # Test non-streaming only:" -ForegroundColor Gray
Write-Host "  .\debug-foundry-api.ps1 -BaseUrl 'http://127.0.0.1:55679' -ModelId 'qwen2.5-0.5b-instruct-openvino-npu:3'" -ForegroundColor Gray
Write-Host ""
Write-Host "  # Test both non-streaming and streaming:" -ForegroundColor Gray
Write-Host "  .\debug-foundry-api.ps1 -BaseUrl 'http://127.0.0.1:55679' -ModelId 'qwen2.5-0.5b-instruct-openvino-npu:3' -Stream" -ForegroundColor Gray
Write-Host ""
Write-Host "  # Test streaming with verbose output:" -ForegroundColor Gray
Write-Host "  .\debug-foundry-api.ps1 -BaseUrl 'http://127.0.0.1:55679' -ModelId 'qwen2.5-0.5b-instruct-openvino-npu:3' -Stream -Verbose" -ForegroundColor Gray
Write-Host ""
Write-Host "  # Custom prompt:" -ForegroundColor Gray
Write-Host "  .\debug-foundry-api.ps1 -Prompt 'Tell me a joke' -Stream" -ForegroundColor Gray
