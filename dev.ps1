<#
.SYNOPSIS
    Starts the development server.

.DESCRIPTION
    Runs Sonarr from source against a data directory of its own, rebuilding and restarting when the
    C# changes. The pieces a plain `dotnet watch run` leaves out are checked first, because each of
    them has already cost an afternoon:

      - `dotnet watch` builds the console project only. Sonarr.Windows.dll belongs to another project,
        and without it the server dies at startup with "Could not load file or assembly". A full build
        of the solution is made when it is missing.
      - The web UI is built separately by webpack. Without it the server starts and serves nothing.
      - A server that is still running holds the output folder open and the build fails on locked files.

.PARAMETER DataDir
    The data directory to run against. Defaults to $env:SONARR_DEV_DATA, then C:\sonarr-mp-data.

.PARAMETER Build
    Build the whole solution before starting, even when nothing looks missing.

.PARAMETER Ui
    Also open a second window that rebuilds the web UI whenever the frontend changes.

.PARAMETER NoWatch
    Run once instead of restarting on changes.

.PARAMETER Stop
    Stop a running development server and exit.

.EXAMPLE
    .\dev.ps1
    .\dev.ps1 -Ui
    .\dev.ps1 -Build -NoWatch
    .\dev.ps1 -Stop
#>
[CmdletBinding()]
param(
    [string] $DataDir = $(if ($env:SONARR_DEV_DATA) { $env:SONARR_DEV_DATA } else { 'C:\sonarr-mp-data' }),
    [switch] $Build,
    [switch] $Ui,
    [switch] $NoWatch,
    [switch] $Stop
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$output = Join-Path $root '_output\net6.0'
$solution = Join-Path $root 'src\Sonarr.sln'
$project = Join-Path $root 'src\NzbDrone.Console\Sonarr.Console.csproj'

# The build reads this to find its own props; without it every project reports hundreds of errors
# that have nothing to do with the code.
$env:SolutionDir = (Join-Path $root 'src') + '\'

function Stop-DevServer {
    $running = Get-Process -Name 'Sonarr.Console', 'Sonarr' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) }

    if ($running) {
        Write-Host "Stopping the running development server (pid $($running.Id -join ', '))" -ForegroundColor Yellow
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
    }
}

Stop-DevServer

if ($Stop) {
    return
}

if (-not (Test-Path $DataDir)) {
    throw "Data directory '$DataDir' does not exist. Pass -DataDir or set SONARR_DEV_DATA."
}

Set-Location $root

$missing = @('Sonarr.Windows.dll', 'Sonarr.Core.dll', 'Sonarr.Console.exe') |
    Where-Object { -not (Test-Path (Join-Path $output $_)) }

if ($Build -or $missing) {
    if ($missing) {
        Write-Host "Missing from the output: $($missing -join ', ')" -ForegroundColor Yellow
    }

    Write-Host 'Building the solution' -ForegroundColor Cyan
    dotnet build $solution -c Debug

    if ($LASTEXITCODE -ne 0) {
        throw 'The build failed.'
    }
}

if (-not (Test-Path (Join-Path $root '_output\UI\index.html'))) {
    Write-Host 'The web UI has not been built, building it' -ForegroundColor Cyan
    yarn build

    if ($LASTEXITCODE -ne 0) {
        throw 'The web UI build failed.'
    }
}

if ($Ui) {
    Write-Host 'Opening a window that rebuilds the web UI on change' -ForegroundColor Cyan
    Start-Process -FilePath 'pwsh' -WorkingDirectory $root -ArgumentList '-NoExit', '-Command', 'yarn start'
}

$port = '?'
$config = Join-Path $DataDir 'config.xml'

if (Test-Path $config) {
    $match = Select-String -Path $config -Pattern '<Port>(\d+)</Port>' | Select-Object -First 1

    if ($match) {
        $port = $match.Matches[0].Groups[1].Value
    }
}

Write-Host "Starting Sonarr on http://localhost:$port  (data: $DataDir)" -ForegroundColor Green

# The data directory has to be a Windows path with backslashes; a forward-slash path is rejected at
# startup as "not a valid Windows path".
$dataArg = '-data=' + $DataDir.Replace('/', '\')

if ($NoWatch) {
    dotnet run --project $project -c Debug -f net6.0 --no-build -- -nobrowser $dataArg
}
else {
    dotnet watch --project $project --no-hot-reload run -c Debug -f net6.0 -- -nobrowser $dataArg
}
