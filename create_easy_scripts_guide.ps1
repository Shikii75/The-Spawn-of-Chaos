$outputPath = Join-Path (Get-Location) 'Game_Dev_OS_Easy_Scripts_Guide.pdf'

$entries = @(
    @{ Section = 'Frontend'; Path = 'index.html'; Purpose = 'Drift Up page structure.' }
    @{ Section = 'Frontend'; Path = 'game.js'; Purpose = 'Canvas game loop, movement, obstacles, and collisions.' }
    @{ Section = 'Frontend'; Path = 'style.css'; Purpose = 'Drift Up styling.' }
    @{ Section = 'Frontend'; Path = 'menu.html'; Purpose = 'Main menu page structure.' }
    @{ Section = 'Frontend'; Path = 'menu.js'; Purpose = 'Menu buttons, particles, and interactions.' }
    @{ Section = 'Frontend'; Path = 'menu.css'; Purpose = 'Main menu styling.' }
    @{ Section = 'Frontend'; Path = 'main-menu-preview.html'; Purpose = 'Canvas menu preview page.' }
    @{ Section = 'Frontend'; Path = 'Assets/Scenes/scripts/style.css'; Purpose = 'Additional web-style presentation rules.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/IDamageable.cs'; Purpose = 'Shared damage interface.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Main Camera/CameraFollow.cs'; Purpose = 'Smooth camera tracking.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Player/health.cs'; Purpose = 'Player health and damage handling.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Player/PlayerCurrency.cs'; Purpose = 'Currency storage and updates.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Player/PlayerFootstepAudio.cs'; Purpose = 'Footstep sound playback.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Coin Drop Prefab/CoinItem.cs'; Purpose = 'Coin pickup behavior.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Cloud Spawner/Cloud.cs'; Purpose = 'Moving cloud behavior.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Hazards/DamageZone.cs'; Purpose = 'Damage-causing area trigger.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Popup Text Prefabs/FloatingDamageNumber.cs'; Purpose = 'Floating damage text.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Popup Text Prefabs/MoveAndFade.cs'; Purpose = 'Popup movement and fading.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Level Goal/LevelGoalTrigger.cs'; Purpose = 'Detects level completion.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/NPC/NPCFacePlayer.cs'; Purpose = 'Makes NPCs face the player.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/NPC/NPCInteractable.cs'; Purpose = 'Basic NPC interaction.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Dojo Entrance/DojoTransitionFader.cs'; Purpose = 'Scene transition fade effect.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Dojo Entrance/DojoDoorTransition.cs'; Purpose = 'Dojo door access and transition.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/NPC/SpeechBubbleDialogue.cs'; Purpose = 'NPC speech bubble dialogue.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Systems/AudioManager.cs'; Purpose = 'Central audio playback manager.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Systems/LevelMusicPlayer.cs'; Purpose = 'Plays level music.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Pause Manager/PauseMenu.cs'; Purpose = 'Pause menu controls.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/UI/TouchControlsManager.cs'; Purpose = 'Mobile control visibility and input.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Minigames/OrbSplash/CollectibleOrb.cs'; Purpose = 'Collectible orb movement and pickup.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Minigames/OrbSplash/OrbSpawner.cs'; Purpose = 'Spawns collectible orbs.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Minigames/MinigameHubUI.cs'; Purpose = 'Minigame selection interface.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Minigames/OrbSplash/OrbSplashArcadeUI.cs'; Purpose = 'Orb Splash minigame UI.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Platforms/DynamicPinkBridgeCluster.cs'; Purpose = 'Activates and animates bridge pieces.' }
    @{ Section = 'Unity C#'; Path = 'Assets/UI/MainMenu/MainMenuUIToolkitController.cs'; Purpose = 'Unity UI Toolkit menu, logo, buttons, and orbs.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Nyxaris Chat Panel/NyxarisFrameAnimator.cs'; Purpose = 'Animates Nyxaris portrait frames.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Nyxaris Chat Panel/NyxarisUIStyler.cs'; Purpose = 'Builds and styles Nyxaris dialogue UI.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Nyxaris Chat Panel/NyxarisManager.cs'; Purpose = 'Handles Nyxaris chat and API communication.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/TutorialCaveWaveManager.cs'; Purpose = 'Controls tutorial enemy waves.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Platforms/PurpleWater2D.cs'; Purpose = 'Simulates interactive spring-based water.' }
    @{ Section = 'Unity C#'; Path = 'Assets/Scenes/scripts/Systems/TutorialLorePrologue.cs'; Purpose = 'Runs the seven-slide opening lore sequence and narration.' }
)

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('THE SPAWN OF CHAOS')
$lines.Add('Game Dev OS and Unity Script Guide')
$lines.Add('')
$lines.Add('Purpose: a ranked list of 40 project files that are easiest to explain.')
$lines.Add('')
$lines.Add('Python note')
$lines.Add('No project-owned Python files were found in the workspace. The Game Dev OS')
$lines.Add('logbook describes Python/Flask work, but the backend source is not present here.')
$lines.Add('')
$lines.Add('The project documentation is in: game_dev_and_ai_training_logbook.md')
$lines.Add('The production plan is in: The_Spawn_of_Chaos_Demo_Roadmap.md')
$lines.Add('')

$lastSection = ''
$number = 0
foreach ($entry in $entries) {
    if ($entry.Section -ne $lastSection) {
        $lines.Add('')
        $lines.Add($entry.Section)
        $lastSection = $entry.Section
    }
    $number++
    $lines.Add(('FILE {0}: {1}' -f $number, $entry.Path))
    $lines.Add(('PURPOSE: {0}' -f $entry.Purpose))
    $lines.Add('')

    $sourcePath = Join-Path (Get-Location) $entry.Path
    if (Test-Path $sourcePath) {
        $sourceText = Get-Content -Raw -LiteralPath $sourcePath
        foreach ($sourceLine in ($sourceText -split "`r?`n")) {
            $safeSourceLine = $sourceLine -replace '[^\x20-\x7E]', '?'
            if ($safeSourceLine.Length -eq 0) {
                $lines.Add('')
                continue
            }
            while ($safeSourceLine.Length -gt 94) {
                $lines.Add(('  ' + $safeSourceLine.Substring(0, 94)))
                $safeSourceLine = '  ' + $safeSourceLine.Substring(94)
            }
            $lines.Add(('  ' + $safeSourceLine))
        }
    }
    else {
        $lines.Add('  SOURCE FILE NOT FOUND IN WORKSPACE')
    }
    $lines.Add('')
}

$pageHeight = 792
$margin = 54
$lineHeight = 9
$maxLines = 76
$pages = @()
for ($start = 0; $start -lt $lines.Count; $start += $maxLines) {
    $pages += ,@($lines[$start..([Math]::Min($start + $maxLines - 1, $lines.Count - 1))])
}

$objects = [System.Collections.Generic.List[string]]::new()
$objects.Add('<< /Type /Catalog /Pages 2 0 R >>')
$pageObjectIds = @()
$fontObjectId = 0
for ($i = 0; $i -lt $pages.Count; $i++) {
    $pageObjectIds += 3 + ($i * 2)
}
$kids = ($pageObjectIds | ForEach-Object { "$_ 0 R" }) -join ' '
$objects.Add("<< /Type /Pages /Kids [$kids] /Count $($pages.Count) >>")

for ($pageIndex = 0; $pageIndex -lt $pages.Count; $pageIndex++) {
    $pageId = 3 + ($pageIndex * 2)
    $contentId = $pageId + 1
    $stream = [System.Text.StringBuilder]::new()
    [void]$stream.AppendLine('BT')
    [void]$stream.AppendLine('/F1 7.5 Tf')
    [void]$stream.AppendLine("$margin $($pageHeight - $margin) Td")
    foreach ($line in $pages[$pageIndex]) {
        $safe = $line.Replace('\', '\\').Replace('(', '\(').Replace(')', '\)')
        [void]$stream.AppendLine("($safe) Tj")
        [void]$stream.AppendLine("0 -$lineHeight Td")
    }
    [void]$stream.AppendLine('ET')
    $streamText = $stream.ToString()
    $objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 $pageHeight] /Resources << /Font << /F1 $($pages.Count * 2 + 3) 0 R >> >> /Contents $contentId 0 R >>")
    $objects.Add("<< /Length $([System.Text.Encoding]::ASCII.GetByteCount($streamText)) >>`nstream`n$streamText`nendstream")
}
$fontObjectId = $objects.Count + 1
$objects.Add('<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>')

$pdf = [System.Text.StringBuilder]::new()
[void]$pdf.AppendLine('%PDF-1.4')
$offsets = [System.Collections.Generic.List[int]]::new()
for ($i = 0; $i -lt $objects.Count; $i++) {
    $offsets.Add([System.Text.Encoding]::ASCII.GetByteCount($pdf.ToString()))
    [void]$pdf.AppendLine("$($i + 1) 0 obj")
    [void]$pdf.AppendLine($objects[$i])
    [void]$pdf.AppendLine('endobj')
}
$xrefOffset = [System.Text.Encoding]::ASCII.GetByteCount($pdf.ToString())
[void]$pdf.AppendLine("xref`n0 $($objects.Count + 1)")
[void]$pdf.AppendLine('0000000000 65535 f ')
foreach ($offset in $offsets) { [void]$pdf.AppendLine(('{0:D10} 00000 n ' -f $offset)) }
[void]$pdf.AppendLine("trailer`n<< /Size $($objects.Count + 1) /Root 1 0 R >>`nstartxref`n$xrefOffset`n%%EOF")
[System.IO.File]::WriteAllBytes($outputPath, [System.Text.Encoding]::ASCII.GetBytes($pdf.ToString()))
Write-Output $outputPath