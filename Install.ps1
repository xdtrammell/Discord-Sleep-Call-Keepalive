$ErrorActionPreference = 'Stop'

$TaskName = 'Discord Sleep Call Keepalive'
$InstallDirectory = Join-Path $env:LOCALAPPDATA 'DiscordSleepCallKeepalive'
$SourceDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceCode = Join-Path $SourceDirectory 'DiscordKeepAlive.cs'
$Executable = Join-Path $InstallDirectory 'DiscordKeepAlive.exe'
$CandidateExecutable = Join-Path $InstallDirectory 'DiscordKeepAlive.candidate.exe'
$BackupExecutable = Join-Path $InstallDirectory 'DiscordKeepAlive.backup.exe'
$SettingsFile = Join-Path $InstallDirectory 'settings.ini'
$BlocklistFile = Join-Path $InstallDirectory 'blocked-processes.txt'
$ReadmeFile = Join-Path $InstallDirectory 'README.md'

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

try {
    Write-Host 'Discord Sleep-Call Keepalive Installer' -ForegroundColor White
    Write-Host 'No administrator access, AutoHotkey, .NET SDK, or game list is required.' -ForegroundColor Gray

    if (-not [Environment]::Is64BitOperatingSystem) {
        throw 'This package requires 64-bit Windows 10 or Windows 11.'
    }

    if (-not (Test-Path -LiteralPath $SourceCode)) {
        throw "The source file is missing: $SourceCode"
    }

    Write-Step 'Creating the local installation folder'
    New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null

    Copy-Item -LiteralPath $SourceCode -Destination (Join-Path $InstallDirectory 'DiscordKeepAlive.cs') -Force
    if (-not (Test-Path -LiteralPath $SettingsFile)) {
        Copy-Item -LiteralPath (Join-Path $SourceDirectory 'settings.ini') -Destination $SettingsFile
    }
    if (-not (Test-Path -LiteralPath $BlocklistFile)) {
        Copy-Item -LiteralPath (Join-Path $SourceDirectory 'blocked-processes.txt') -Destination $BlocklistFile
    }
    Copy-Item -LiteralPath (Join-Path $SourceDirectory 'README.md') -Destination $ReadmeFile -Force

    Write-Step 'Compiling the purpose-built keepalive executable'
    if (Test-Path -LiteralPath $CandidateExecutable) {
        Remove-Item -LiteralPath $CandidateExecutable -Force
    }

    Add-Type -Path (Join-Path $InstallDirectory 'DiscordKeepAlive.cs') `
        -OutputAssembly $CandidateExecutable `
        -OutputType WindowsApplication

    if (-not (Test-Path -LiteralPath $CandidateExecutable)) {
        throw 'Windows did not produce DiscordKeepAlive.exe.'
    }

    Write-Step 'Checking the native Windows input layout'
    $SelfTest = Start-Process -FilePath $CandidateExecutable -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
    if ($SelfTest.ExitCode -ne 0) {
        throw "The compiled executable did not pass its self-test. See $InstallDirectory\status.txt."
    }
    Get-Content -LiteralPath (Join-Path $InstallDirectory 'status.txt')

    if (Test-Path -LiteralPath $BackupExecutable) {
        Remove-Item -LiteralPath $BackupExecutable -Force
    }
    if (Test-Path -LiteralPath $Executable) {
        Move-Item -LiteralPath $Executable -Destination $BackupExecutable
    }
    try {
        Move-Item -LiteralPath $CandidateExecutable -Destination $Executable
    }
    catch {
        if (Test-Path -LiteralPath $BackupExecutable) {
            Move-Item -LiteralPath $BackupExecutable -Destination $Executable
        }
        throw
    }

    Write-Step 'Registering the lightweight scheduled task'
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $rootFolder = $service.GetFolder('\')
    $definition = $service.NewTask(0)

    $definition.RegistrationInfo.Description = 'Checks every 15 minutes and sends F15 only after 165 minutes of genuine user inactivity while Discord is running.'
    $definition.RegistrationInfo.Author = $env:USERNAME

    $CurrentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $definition.Principal.UserId = $CurrentUser
    $definition.Principal.LogonType = 3 # TASK_LOGON_INTERACTIVE_TOKEN
    $definition.Principal.RunLevel = 0 # TASK_RUNLEVEL_LUA

    $trigger = $definition.Triggers.Create(2) # TASK_TRIGGER_DAILY
    $trigger.StartBoundary = (Get-Date).AddMinutes(1).ToString('yyyy-MM-ddTHH:mm:ss')
    $trigger.DaysInterval = 1
    $trigger.Enabled = $true
    $trigger.Repetition.Interval = 'PT15M'
    $trigger.Repetition.Duration = 'P1D'
    $trigger.Repetition.StopAtDurationEnd = $false

    $action = $definition.Actions.Create(0) # TASK_ACTION_EXEC
    $action.Path = $Executable
    $action.Arguments = '--run'
    $action.WorkingDirectory = $InstallDirectory

    $definition.Settings.Enabled = $true
    $definition.Settings.Hidden = $false
    $definition.Settings.StartWhenAvailable = $true
    $definition.Settings.DisallowStartIfOnBatteries = $false
    $definition.Settings.StopIfGoingOnBatteries = $false
    $definition.Settings.AllowDemandStart = $true
    $definition.Settings.ExecutionTimeLimit = 'PT1M'
    $definition.Settings.MultipleInstances = 2 # TASK_INSTANCES_IGNORE_NEW

    # TASK_CREATE_OR_UPDATE = 6, TASK_LOGON_INTERACTIVE_TOKEN = 3
    $null = $rootFolder.RegisterTaskDefinition($TaskName, $definition, 6, $CurrentUser, $null, 3, $null)

    Write-Step 'Running a non-input diagnostic check'
    $Diagnostic = Start-Process -FilePath $Executable -ArgumentList '--status' -Wait -PassThru -WindowStyle Hidden
    if ($Diagnostic.ExitCode -ne 0) {
        throw "The diagnostic check returned exit code $($Diagnostic.ExitCode)."
    }
    Get-Content -LiteralPath (Join-Path $InstallDirectory 'status.txt')

    if (Test-Path -LiteralPath $BackupExecutable) {
        Remove-Item -LiteralPath $BackupExecutable -Force
    }

    Write-Host "`nInstallation complete." -ForegroundColor Green
    Write-Host "Installed folder: $InstallDirectory"
    Write-Host "Scheduled task:   $TaskName"
    Write-Host 'The task will check automatically every 15 minutes.'
    Write-Host 'It only sends F15 after 165 minutes of genuine inactivity while Discord is running.'
    Write-Host "`nPress Enter to close this window."
    [void](Read-Host)
}
catch {
    $InstallError = $_.Exception.Message
    $InstallStackTrace = $_.ScriptStackTrace
    # Keep the previously working build if an upgrade fails after the swap.
    if (Test-Path -LiteralPath $BackupExecutable) {
        try {
            if (Test-Path -LiteralPath $Executable) {
                Move-Item -LiteralPath $Executable -Destination $CandidateExecutable -Force
            }
            Move-Item -LiteralPath $BackupExecutable -Destination $Executable
            Write-Host 'The previously installed executable was restored.' -ForegroundColor Yellow
        }
        catch {
            Write-Host "Automatic rollback failed. The previous executable is at $BackupExecutable." -ForegroundColor Red
        }
    }
    Write-Host "`nInstallation failed: $InstallError" -ForegroundColor Red
    Write-Host "`nNothing was intentionally hidden. The full error is below:" -ForegroundColor Yellow
    Write-Host $InstallStackTrace -ForegroundColor DarkGray
    Write-Host "`nPress Enter to close this window."
    [void](Read-Host)
    exit 1
}
