param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [ValidateSet('All', 'Validate', 'EditMode', 'PlayMode')]
    [string]$Mode = 'All',
    [string]$TestFilter = 'Starter.Tests',
    [string]$OutputRoot = 'E:\AI\_DownLoads\unity-gf-demos\build\verification',
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity editor not found: $UnityPath" }
if ($TimeoutSeconds -lt 30) { throw 'TimeoutSeconds must be at least 30.' }
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$runPath = Join-Path $OutputRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $runPath -ErrorAction Stop | Out-Null
$processTemp = Join-Path $runPath 'process-temp'
New-Item -ItemType Directory -Path $processTemp | Out-Null
$oldTemp = $env:TEMP
$oldTmp = $env:TMP

function Invoke-UnityStep([string]$Step, [string[]]$StepArguments, [string]$ResultsPath = '') {
    $logPath = Join-Path $runPath ($Step + '.log')
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'), '-logFile', ('"' + $logPath + '"')) + $StepArguments
    Write-Output "Starting $Step. Log: $logPath"
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $nextProgress = 30
    $completedAt = $null
    $recoveredShutdown = $false
    while (-not $process.WaitForExit(500)) {
        if ($ResultsPath -and (Test-Path -LiteralPath $ResultsPath -PathType Leaf)) {
            try {
                [xml]$writtenResult = Get-Content -LiteralPath $ResultsPath -Raw
                if ($writtenResult.'test-run'.'end-time' -and $null -eq $completedAt) {
                    $completedAt = $watch.Elapsed.TotalSeconds
                }
            }
            catch {
                # The result writer may still be writing the XML document.
            }
        }
        if ($null -ne $completedAt -and $watch.Elapsed.TotalSeconds - $completedAt -ge 20) {
            # Only stop the process created here, after the runner wrote a complete result.
            Write-Warning "$Step wrote its final result but Unity did not exit within 20s. Stopping owned runner PID $($process.Id); test results are checked separately."
            Stop-Process -Id $process.Id -ErrorAction Stop
            $process.WaitForExit(5000) | Out-Null
            $recoveredShutdown = $true
            break
        }
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
            Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            throw "Unity $Step timed out. Inspect $logPath"
        }
        if ($watch.Elapsed.TotalSeconds -ge $nextProgress) {
            Write-Output "$Step is running ($([int]$watch.Elapsed.TotalSeconds)s)."
            $nextProgress += 30
        }
    }
    if (-not $recoveredShutdown -and $process.ExitCode -ne 0) { throw "Unity $Step exited with code $($process.ExitCode). Inspect $logPath" }
    Write-Output "$Step completed in $([int]$watch.Elapsed.TotalSeconds)s."
}

try {
    $env:TEMP = $processTemp
    $env:TMP = $processTemp
    if ($Mode -in @('All', 'Validate')) {
        Invoke-UnityStep 'validate' @('-quit', '-executeMethod', 'Starter.Editor.StarterProjectBuilder.BuildDemo')
        if (-not (Select-String -LiteralPath (Join-Path $runPath 'validate.log') -SimpleMatch 'STARTER_VALIDATED:' -Quiet)) {
            throw 'Unity exited without the expected validation marker.'
        }
        Invoke-UnityStep 'extensions' @('-quit', '-executeMethod', 'Starter.Extensions.Samples.Editor.ExtensionsSampleBuilder.BuildGallery')
        if (-not (Select-String -LiteralPath (Join-Path $runPath 'extensions.log') -SimpleMatch 'STARTER_EXTENSIONS_VALIDATED:' -Quiet)) {
            throw 'Unity exited without the extension validation marker.'
        }
    }
    foreach ($testPlatform in @('EditMode', 'PlayMode')) {
        if ($Mode -notin @('All', $testPlatform)) { continue }
        $resultsPath = Join-Path $runPath ($testPlatform + '.xml')
        # Do not pass -quit: Unity Test Runner exits after writing the result file.
        Invoke-UnityStep $testPlatform @('-runTests', '-testPlatform', $testPlatform.ToLowerInvariant(), '-testFilter', $TestFilter, '-testResults', ('"' + $resultsPath + '"')) $resultsPath
        if (-not (Test-Path -LiteralPath $resultsPath)) { throw "Missing test result: $resultsPath" }
        [xml]$results = Get-Content -LiteralPath $resultsPath -Raw
        $run = $results.'test-run'
        if ($null -eq $run -or [int]$run.total -lt 1 -or [int]$run.failed -ne 0 -or $run.result -ne 'Passed') {
            throw "Unity $testPlatform did not pass. Inspect $resultsPath"
        }
        Write-Output "$testPlatform results: $($run.passed) passed, $($run.failed) failed, $($run.skipped) skipped."
    }
    Write-Output "Verification reports: $runPath"
}
finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
}
