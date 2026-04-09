# Kaya WPF Release Script
# Automates the release process:
#   Step 1: Determine and confirm new version
#   Step 2: Update version in source files
#   Step 3: Commit, tag, and push

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot

function Run-Git {
    param([string]$Command)
    $output = cmd /c "git -C `"$Root`" $Command 2>&1"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Command failed: git $Command`n$($output -join "`n")"
        exit 1
    }
    return ($output -join "`n").Trim()
}

function Confirm-Prompt {
    param([string]$Message)
    $answer = Read-Host "$Message [Y/n]"
    if ($answer -ne "" -and $answer.ToLower() -ne "y") {
        Write-Host "Aborted."
        exit 0
    }
}

# --- Step 1: Determine new version ---

Write-Host ""
Write-Host "=== Step 1: Determine new version ==="
Write-Host ""

$allTags = @((Run-Git "tag --sort=-v:refname") -split "`n" |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -match "^v\d+\.\d+\.\d+$" })

if ($allTags.Count -eq 0) {
    $newVersion = Read-Host "No version tags found. Initial version (e.g. 0.1.0)"
} else {
    $currentTag = $allTags[0]
    $currentVersion = $currentTag -replace "^v", ""
    $parts = $currentVersion -split "\."
    $suggestedVersion = "$($parts[0]).$($parts[1]).$([int]$parts[2] + 1)"

    Write-Host "Current version: $currentVersion ($currentTag)"
    Write-Host "Suggested version: $suggestedVersion"
    Write-Host ""

    $input = Read-Host "New version [$suggestedVersion]"
    $newVersion = if ($input -eq "") { $suggestedVersion } else { $input }
}

if ($newVersion -notmatch "^\d+\.\d+\.\d+$") {
    Write-Error "Invalid version format: $newVersion (expected X.Y.Z)"
    exit 1
}

$newTag = "v$newVersion"

Write-Host ""
Write-Host "Will release: $newVersion ($newTag)"
Confirm-Prompt "Proceed?"

# --- Step 2: Update version in source files ---

Write-Host ""
Write-Host "=== Step 2: Update version in source files ==="
Write-Host ""

# AboutWindow.xaml
$aboutPath = Join-Path $Root "src/Kaya.Wpf/AboutWindow.xaml"
$aboutContent = Get-Content $aboutPath -Raw
$aboutContent = $aboutContent -replace 'Version \d+\.\d+\.\d+', "Version $newVersion"
Set-Content -Path $aboutPath -Value $aboutContent -NoNewline
Write-Host "  Updated AboutWindow.xaml"

Write-Host ""
Run-Git "diff" | Write-Host
Write-Host ""

Confirm-Prompt "Diffs look correct?"

# --- Step 3: Commit, tag, and push ---

Write-Host ""
Write-Host "=== Step 3: Commit, tag, and push ==="
Write-Host ""

Run-Git "add ."
Run-Git "commit -m `"cut a new version: $newVersion`""
Write-Host "  Committed: cut a new version: $newVersion"

Run-Git "tag -a $newTag -m `"Release $newTag`""
Write-Host "  Tagged: $newTag"

Write-Host ""
Confirm-Prompt "Push (with tags)?"
Run-Git "push"
Run-Git "push --tags"
Write-Host "  Pushed to remote"

Write-Host ""
Write-Host "=== Release $newVersion complete ==="
