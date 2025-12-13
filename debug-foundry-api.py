#!/usr/bin/env python3
"""
FoundryLocal API Debug Script (Python version)
This script tests the FoundryLocal chat completions endpoint
"""

import requests
import json
import time
import sys
from typing import Optional

# Configuration
BASE_URL = "http://127.0.0.1:55679"
MODEL_ID = "qwen2.5-0.5b-instruct-openvino-npu:3"
PROMPT = "Hello, how are you?"

def print_header(text: str):
    print(f"\n{'=' * 60}")
    print(f"  {text}")
    print(f"{'=' * 60}\n")

def print_section(text: str):
    print(f"\n[{text}]")
    print("-" * 60)

def test_service_health(base_url: str) -> bool:
    """Test if the FoundryLocal service is running"""
    print_section("Test 1: Service Health Check")
    
    try:
        response = requests.get(f"{base_url}/v1/models", timeout=5)
        print(f"✓ Service is running")
        print(f"Status Code: {response.status_code}")
        print(f"Response:\n{json.dumps(response.json(), indent=2)}")
        return True
    except requests.exceptions.RequestException as e:
        print(f"✗ Service is NOT running or not accessible")
        print(f"Error: {e}")
        return False

def test_non_streaming(base_url: str, model_id: str, prompt: str):
    """Test non-streaming chat completion"""
    print_section("Test 2: Non-Streaming Chat Completion")
    
    request_body = {
        "model": model_id,
        "messages": [
            {
                "role": "system",
                "content": "You are a helpful assistant."
            },
            {
                "role": "user",
                "content": prompt
            }
        ],
        "stream": False,
        "temperature": 0.7,
        "max_tokens": 100
    }
    
    print(f"Request Body:\n{json.dumps(request_body, indent=2)}\n")
    
    try:
        start_time = time.time()
        response = requests.post(
            f"{base_url}/v1/chat/completions",
            json=request_body,
            timeout=30
        )
        elapsed_ms = (time.time() - start_time) * 1000
        
        print(f"✓ Non-streaming request successful")
        print(f"Status Code: {response.status_code}")
        print(f"Time taken: {elapsed_ms:.0f}ms")
        
        print(f"\nResponse Headers:")
        for key, value in response.headers.items():
            print(f"  {key}: {value}")
        
        if response.status_code == 200:
            result = response.json()
            print(f"\nResponse Body:\n{json.dumps(result, indent=2)}")
            
            if result.get('choices') and result['choices'][0].get('message', {}).get('content'):
                print(f"\n{'=' * 60}")
                print(f"Generated Text:")
                print(f"{'=' * 60}")
                print(result['choices'][0]['message']['content'])
                print(f"{'=' * 60}")
        else:
            print(f"\n✗ Error Response:\n{response.text}")
            
    except requests.exceptions.RequestException as e:
        print(f"✗ Non-streaming request failed")
        print(f"Error: {e}")

def test_streaming(base_url: str, model_id: str, prompt: str):
    """Test streaming chat completion"""
    print_section("Test 3: Streaming Chat Completion")
    
    request_body = {
        "model": model_id,
        "messages": [
            {
                "role": "system",
                "content": "You are a helpful assistant."
            },
            {
                "role": "user",
                "content": prompt
            }
        ],
        "stream": True,
        "stream_options": {
            "include_usage": True
        },
        "temperature": 0.7,
        "max_tokens": 100
    }
    
    print(f"Request Body:\n{json.dumps(request_body, indent=2)}\n")
    
    try:
        print("Sending streaming request...")
        start_time = time.time()
        
        response = requests.post(
            f"{base_url}/v1/chat/completions",
            json=request_body,
            stream=True,
            timeout=60
        )
        
        print(f"Response Status: {response.status_code}")
        print(f"\nResponse Headers:")
        for key, value in response.headers.items():
            print(f"  {key}: {value}")
        
        if response.status_code != 200:
            print(f"\n✗ Streaming request failed with status {response.status_code}")
            print(f"Error response: {response.text}")
            return
        
        print(f"\n✓ Streaming connection established")
        print(f"Reading stream...\n")
        print(f"{'=' * 60}")
        print("Stream Output:")
        print(f"{'=' * 60}\n")
        
        chunk_count = 0
        total_bytes = 0
        first_chunk_time = None
        generated_text = ""
        
        try:
            for line in response.iter_lines(decode_unicode=True):
                if not line:
                    continue
                
                total_bytes += len(line.encode('utf-8'))
                
                if chunk_count == 0:
                    first_chunk_time = time.time() - start_time
                    print(f"[First chunk received after {first_chunk_time:.3f}s]\n")
                
                chunk_count += 1
                
                # Print raw line for debugging
                print(f"Raw: {line}")
                
                # Parse SSE data
                if line.startswith("data: "):
                    data_str = line[6:]  # Remove "data: " prefix
                    
                    if data_str == "[DONE]":
                        print("\n[Stream marked as DONE]")
                        break
                    
                    try:
                        data = json.loads(data_str)
                        
                        # Extract content delta
                        if data.get('choices'):
                            delta = data['choices'][0].get('delta', {})
                            content = delta.get('content', '')
                            
                            if content:
                                generated_text += content
                                print(f"Content: {content}", end='', flush=True)
                        
                        # Print usage if available
                        if data.get('usage'):
                            print(f"\n\nUsage: {json.dumps(data['usage'], indent=2)}")
                            
                    except json.JSONDecodeError as e:
                        print(f"\n[JSON Parse Error: {e}]")
                        print(f"[Problematic data: {data_str}]")
            
            elapsed = time.time() - start_time
            
            print(f"\n\n{'=' * 60}")
            print("Stream Statistics:")
            print(f"{'=' * 60}")
            print(f"✓ Stream completed successfully")
            print(f"Total chunks: {chunk_count}")
            print(f"Total bytes: {total_bytes}")
            print(f"Total time: {elapsed:.3f}s")
            if first_chunk_time:
                print(f"Time to first chunk: {first_chunk_time:.3f}s")
            
            if generated_text:
                print(f"\n{'=' * 60}")
                print("Complete Generated Text:")
                print(f"{'=' * 60}")
                print(generated_text)
                print(f"{'=' * 60}")
                
        except Exception as e:
            elapsed = time.time() - start_time
            print(f"\n\n✗ Error while reading stream")
            print(f"Error: {e}")
            print(f"Error Type: {type(e).__name__}")
            print(f"Chunks received before error: {chunk_count}")
            print(f"Bytes received before error: {total_bytes}")
            print(f"Time elapsed: {elapsed:.3f}s")
            
            # Print partial generated text if any
            if generated_text:
                print(f"\nPartial Generated Text:")
                print(f"{'=' * 60}")
                print(generated_text)
                print(f"{'=' * 60}")
            
    except requests.exceptions.RequestException as e:
        print(f"✗ Streaming request failed")
        print(f"Error: {e}")
        print(f"Error Type: {type(e).__name__}")

def main():
    print_header("FoundryLocal API Debug Script (Python)")
    
    print(f"Configuration:")
    print(f"  Base URL: {BASE_URL}")
    print(f"  Model ID: {MODEL_ID}")
    print(f"  Prompt: {PROMPT}")
    
    # Test 1: Service health
    if not test_service_health(BASE_URL):
        print("\n⚠ Service is not available. Exiting.")
        sys.exit(1)
    
    # Test 2: Non-streaming
    test_non_streaming(BASE_URL, MODEL_ID, PROMPT)
    
    # Test 3: Streaming
    test_streaming(BASE_URL, MODEL_ID, PROMPT)
    
    print_header("Debug Script Completed")
    print("\nUsage:")
    print("  1. Make sure FoundryLocal service is running")
    print("  2. Update BASE_URL, MODEL_ID, and PROMPT variables in the script")
    print("  3. Run: python debug-foundry-api.py")
    print("\nRequirements:")
    print("  pip install requests")

if __name__ == "__main__":
    main()
