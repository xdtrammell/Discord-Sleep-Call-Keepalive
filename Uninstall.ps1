$ErrorActionPreference = 'Stop'

$TaskName = 'Discord Sleep Call Keepalive'
$InstallDirectory = Join-Path $env:LOCALAPPDATA 'DiscordSleepCallKeepalive'

try {
    Write-Host 'Discord Sleep-Call Keepalive Uninstaller' -ForegroundColor White

    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $rootFolder = $service.GetFolder('\')

    try {
        $rootFolder.DeleteTask($TaskName, 0)
        Write-Host "Removed scheduled task: $TaskName" -ForegroundColor Green
    }
    catch {
        if ($_.Exception.Message -match 'cannot find|does not exist|0x80070002') {
            Write-Host 'The scheduled task was already absent.' -ForegroundColor Yellow
        }
        else {
            throw
        }
    }

    if (Test-Path -LiteralPath $InstallDirectory) {
        Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
        Write-Host "Removed folder: $InstallDirectory" -ForegroundColor Green
    }
    else {
        Write-Host 'The installation folder was already absent.' -ForegroundColor Yellow
    }

    Write-Host "`nUninstall complete. Press Enter to close."
    [void](Read-Host)
}
catch {
    Write-Host "`nUninstall failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nPress Enter to close this window."
    [void](Read-Host)
    exit 1
}
