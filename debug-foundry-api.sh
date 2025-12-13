# FoundryLocal API Debug - curl Commands
# Copy and paste these commands in your terminal to test the API

## Configuration
# Update these values based on your FoundryLocal instance
BASE_URL="http://127.0.0.1:55679"
MODEL_ID="qwen2.5-0.5b-instruct-openvino-npu:3"

## Test 1: Check if service is running
echo "=== Test 1: Service Health Check ==="
curl -v "$BASE_URL/v1/models"

## Test 2: Non-streaming chat completion
echo ""
echo "=== Test 2: Non-Streaming Chat Completion ==="
curl -v -X POST "$BASE_URL/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "'"$MODEL_ID"'",
    "messages": [
      {
        "role": "system",
        "content": "You are a helpful assistant."
      },
      {
        "role": "user",
        "content": "Hello, how are you?"
      }
    ],
    "stream": false,
    "temperature": 0.7,
    "max_tokens": 100
  }'

## Test 3: Streaming chat completion
echo ""
echo "=== Test 3: Streaming Chat Completion ==="
curl -v -N -X POST "$BASE_URL/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "'"$MODEL_ID"'",
    "messages": [
      {
        "role": "system",
        "content": "You are a helpful assistant."
      },
      {
        "role": "user",
        "content": "Tell me a short joke"
      }
    ],
    "stream": true,
    "stream_options": {
      "include_usage": true
    },
    "temperature": 0.7,
    "max_tokens": 100
  }'

# Notes:
# - The -v flag shows verbose output including headers
# - The -N flag disables buffering for streaming responses
# - Update BASE_URL and MODEL_ID variables at the top
# 
# PowerShell equivalent (run in PowerShell):
# 
# $BaseUrl = "http://127.0.0.1:55679"
# $ModelId = "qwen2.5-0.5b-instruct-openvino-npu:3"
# 
# # Non-streaming
# $body = @{
#     model = $ModelId
#     messages = @(
#         @{ role = "system"; content = "You are a helpful assistant." }
#         @{ role = "user"; content = "Hello, how are you?" }
#     )
#     stream = $false
#     temperature = 0.7
#     max_tokens = 100
# } | ConvertTo-Json -Depth 10
# 
# Invoke-RestMethod -Uri "$BaseUrl/v1/chat/completions" -Method POST -ContentType "application/json" -Body $body
