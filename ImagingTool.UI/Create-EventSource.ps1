# Create-EventSource.ps1
# Creates an Event Source for POSVolumeInit application

param(
    [string]$SourceName = "POSVolumeInit",
    [string]$LogName = "Application"
)

Write-Host "Creating Event Source: $SourceName in $LogName log..."

try {
    # Check if the event source already exists
    if ([System.Diagnostics.EventLog]::SourceExists($SourceName)) {
        Write-Host "Event Source '$SourceName' already exists."
        
        # Verify it's registered to the correct log
        $currentLog = [System.Diagnostics.EventLog]::LogNameFromSourceName($SourceName, ".")
        if ($currentLog -eq $LogName) {
            Write-Host "Event Source is already registered to the correct log: $LogName"
            exit 0
        }
        else {
            Write-Host "Warning: Event Source exists but is registered to log: $currentLog"
            Write-Host "To change the log, the source must be deleted and recreated."
            Write-Host "Deleting existing event source..."
            [System.Diagnostics.EventLog]::DeleteEventSource($SourceName)
        }
    }
    
    # Create the event source
    Write-Host "Creating new Event Source '$SourceName' in log '$LogName'..."
    New-EventLog -LogName $LogName -Source $SourceName -ErrorAction Stop
    
    Write-Host "Successfully created Event Source: $SourceName"
    
    # Write a test entry to verify it works
    Write-EventLog -LogName $LogName -Source $SourceName -EventId 1000 -EntryType Information -Message "Event Source '$SourceName' created successfully by ImagingTool" -ErrorAction Stop
    
    Write-Host "Test event written successfully to Event Log"
    exit 0
}
catch {
    Write-Host "Error creating Event Source: $_" -ForegroundColor Red
    Write-Host "Exception Details: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}