<#
.SYNOPSIS
    Monitors the AI Support Workflow via SSE stream or agent status endpoint.

.DESCRIPTION
    Connects to the AI Support Workflow API to either stream workflow state
    updates via Server-Sent Events or display current agent statuses.

.PARAMETER BaseUrl
    The base URL of the running API. Defaults to http://localhost:5080.

.PARAMETER Agents
    When specified, fetches and displays current agent statuses as a table, then exits.

.EXAMPLE
    .\Monitor-Workflow.ps1
    Connects to the SSE stream at http://localhost:5080/api/support/stream.

.EXAMPLE
    .\Monitor-Workflow.ps1 -BaseUrl http://localhost:5090
    Connects to the SSE stream at a custom base URL.

.EXAMPLE
    .\Monitor-Workflow.ps1 -Agents
    Displays current agent statuses in a formatted table.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5080",
    [switch]$Agents
)

$ErrorActionPreference = "Stop"

function Show-AgentStatuses {
    $url = "$BaseUrl/api/support/agents"
    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
        if ($response) {
            $response | Format-Table -AutoSize
        }
        else {
            Write-Host "No agents found."
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            Write-Host "Visualization is disabled. Enable it by setting Workflow:EnableVisualization to true in appsettings.json."
        }
        else {
            Write-Error "Failed to connect to $url. Ensure the API is running at $BaseUrl."
            exit 1
        }
    }
}

function Start-SseStream {
    $url = "$BaseUrl/api/support/stream"
    $httpClient = $null
    $stream = $null
    $reader = $null

    try {
        $httpClient = New-Object System.Net.Http.HttpClient
        $httpClient.Timeout = [System.Threading.Timeout]::InfiniteTimeSpan

        $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, $url)
        $request.Headers.Accept.Add([System.Net.Http.Headers.MediaTypeWithQualityHeaderValue]::new("text/event-stream"))

        Write-Host "Connecting to SSE stream at $url ..." -ForegroundColor Cyan
        Write-Host "Press Ctrl+C to stop." -ForegroundColor DarkGray
        Write-Host ""

        $response = $httpClient.SendAsync(
            $request,
            [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead
        ).GetAwaiter().GetResult()

        if ($response.StatusCode -eq [System.Net.HttpStatusCode]::NotFound) {
            Write-Host "Visualization is disabled. Enable it by setting Workflow:EnableVisualization to true in appsettings.json."
            return
        }

        $response.EnsureSuccessStatusCode() | Out-Null

        $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $reader = New-Object System.IO.StreamReader($stream)

        while (-not $reader.EndOfStream) {
            $line = $reader.ReadLineAsync().GetAwaiter().GetResult()
            if ($line -and $line.StartsWith("data:")) {
                $data = $line.Substring(5).Trim()
                $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                Write-Host "[$timestamp] $data"
            }
        }
    }
    catch [System.Net.Http.HttpRequestException] {
        Write-Error "Failed to connect to $url. Ensure the API is running at $BaseUrl."
        exit 1
    }
    catch [System.AggregateException] {
        $inner = $_.Exception.InnerException
        if ($inner -is [System.Net.Http.HttpRequestException]) {
            Write-Error "Failed to connect to $url. Ensure the API is running at $BaseUrl."
            exit 1
        }
        throw
    }
    finally {
        if ($reader) { $reader.Dispose() }
        if ($stream) { $stream.Dispose() }
        if ($httpClient) { $httpClient.Dispose() }
    }
}

# Main
if ($Agents) {
    Show-AgentStatuses
}
else {
    Start-SseStream
}
