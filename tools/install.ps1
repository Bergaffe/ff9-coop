<#
.SYNOPSIS
  Installiert (oder entfernt) den FF9Coop-Mod-Ordner im Spiel und traegt ihn in Memoria.ini ein.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\install.ps1
  powershell -ExecutionPolicy Bypass -File tools\install.ps1 -Uninstall
  powershell -ExecutionPolicy Bypass -File tools\install.ps1 -GameDir "D:\SteamLibrary\steamapps\common\FINAL FANTASY IX"
#>
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY IX',
    [switch]$Uninstall,
    # Ini-Datei des Mods beim Update nicht ueberschreiben (eigene Einstellungen behalten)
    [switch]$KeepIni = $true
)

$ErrorActionPreference = 'Stop'
$ModName = 'FF9Coop'
$Source = Join-Path $PSScriptRoot "..\mod\$ModName"
$Target = Join-Path $GameDir $ModName
$Ini = Join-Path $GameDir 'Memoria.ini'

if (-not (Test-Path $Ini)) { throw "Memoria.ini nicht gefunden in '$GameDir'. Ist Memoria installiert?" }

# --- Memoria.ini lesen (Kodierung inkl. BOM beibehalten) ---
$bytes = [IO.File]::ReadAllBytes($Ini)
$hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
$text = [IO.File]::ReadAllText($Ini)

$backup = "$Ini.ff9coop.bak"
if (-not (Test-Path $backup)) {
    Copy-Item $Ini $backup
    Write-Host "Backup: $backup"
}

function Update-ModList([string]$content, [string]$key, [bool]$add) {
    # [^\r\n]* statt .* : bei CRLF-Dateien sonst das \r mitersetzen
    $pattern = "(?m)^([ \t]*$key[ \t]*=[ \t]*)([^\r\n]*)"
    $m = [regex]::Match($content, $pattern)
    if (-not $m.Success) {
        if ($add) { Write-Warning "$key nicht in Memoria.ini gefunden - bitte '$ModName' manuell im Launcher aktivieren." }
        return $content
    }
    $items = @([regex]::Matches($m.Groups[2].Value, '"([^"]*)"') | ForEach-Object { $_.Groups[1].Value })
    $items = @($items | Where-Object { $_ -ne $ModName })
    if ($add) { $items = @($ModName) + $items }
    $joined = ($items | ForEach-Object { '"' + $_ + '"' }) -join ', '
    return $content.Substring(0, $m.Groups[2].Index) + $joined + $content.Substring($m.Groups[2].Index + $m.Groups[2].Length)
}

$text = Update-ModList $text 'FolderNames' (-not $Uninstall)
$text = Update-ModList $text 'Priorities' (-not $Uninstall)
[IO.File]::WriteAllText($Ini, $text, (New-Object Text.UTF8Encoding($hasBom)))

if ($Uninstall) {
    if (Test-Path $Target) { Remove-Item $Target -Recurse -Force }
    Write-Host "$ModName entfernt und aus Memoria.ini ausgetragen."
    return
}

if (-not (Test-Path (Join-Path $Source "StreamingAssets\Scripts\Memoria.Scripts.$ModName.dll"))) {
    throw "Build fehlt. Erst bauen: dotnet build -c Release src\FF9Coop"
}

New-Item -ItemType Directory -Force $Target | Out-Null
Get-ChildItem $Source -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring((Resolve-Path $Source).Path.Length).TrimStart('\')
    $dest = Join-Path $Target $rel
    if ($KeepIni -and $rel -eq "$ModName.ini" -and (Test-Path $dest)) { return }
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Copy-Item $_.FullName $dest -Force
}
Write-Host "$ModName installiert nach $Target"
Write-Host "Memoria.ini FolderNames:" ([regex]::Match($text, '(?m)^\s*FolderNames\s*=.*$').Value.Trim())
