"""
Python FastAPI proxy server for the C# backend.
Routes all API calls to the C# backend running on port 8002.
"""
from fastapi import FastAPI, Request, Response
from fastapi.middleware.cors import CORSMiddleware
import httpx
import os

app = FastAPI()

# CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

CSHARP_BACKEND_URL = "http://localhost:8002"

@app.api_route("/{path:path}", methods=["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS"])
async def proxy(path: str, request: Request):
    """Proxy all requests to the C# backend."""
    async with httpx.AsyncClient(timeout=120.0) as client:
        url = f"{CSHARP_BACKEND_URL}/{path}"
        
        # Get request body
        body = await request.body()
        
        # Forward headers (except host)
        headers = dict(request.headers)
        headers.pop("host", None)
        
        try:
            response = await client.request(
                method=request.method,
                url=url,
                content=body,
                headers=headers,
                params=dict(request.query_params)
            )
            
            return Response(
                content=response.content,
                status_code=response.status_code,
                headers=dict(response.headers)
            )
        except Exception as e:
            return Response(
                content=f'{{"detail": "Backend unavailable: {str(e)}"}}',
                status_code=503,
                media_type="application/json"
            )
