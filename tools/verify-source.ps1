param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @(
    'DDay3.csproj', 'App.xaml', 'App.xaml.cs',
    'Views\MainWindow.xaml', 'Views\MainWindow.xaml.cs',
    'Views\SettingsWindow.xaml', 'Views\SettingsWindow.xaml.cs',
    'Diagnostics\IDiagnosticLog.cs', 'Diagnostics\NullDiagnosticLog.cs',
    'Diagnostics\DiagnosticLogFactory.cs', 'Diagnostics\DeveloperTraceLog.cs',
    'Assets\DDay3.png', 'Assets\DDay3.ico'
)

foreach ($relative in $required) {
    if (-not (Test-Path (Join-Path $root $relative))) {
        throw "Required file missing: $relative"
    }
}

[xml]$project = Get-Content -Encoding UTF8 (Join-Path $root 'DDay3.csproj')
$projectText = Get-Content -Encoding UTF8 (Join-Path $root 'DDay3.csproj') -Raw
if ($projectText -notmatch 'TargetFramework>net48<') { throw 'TargetFramework must be net48.' }
if ($projectText -notmatch 'Compile Remove="Diagnostics\\DeveloperTraceLog.cs"') {
    throw 'Release diagnostics exclusion is missing.'
}
if ($projectText -notmatch 'TRACE_DIAGNOSTICS') { throw 'Debug diagnostic symbol is missing.' }
if ($projectText -notmatch '<Version>3\.1\.6</Version>') { throw 'Project version is not 3.1.6.' }

$source = Get-ChildItem $root -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](tests|obj|bin)[\\/]' } | Get-Content -Raw -Encoding UTF8
if ($source -match 'PySide6|PyInstaller') { throw 'Python runtime reference found in C# sources.' }
if ($source -match 'Newtonsoft|Serilog|NLog') { throw 'Unapproved external dependency reference found.' }

$mainXaml = Get-Content -Encoding UTF8 (Join-Path $root 'Views\MainWindow.xaml') -Raw
$mainCode = Get-Content -Encoding UTF8 (Join-Path $root 'Views\MainWindow.xaml.cs') -Raw
$settingsXaml = Get-Content -Encoding UTF8 (Join-Path $root 'Views\SettingsWindow.xaml') -Raw
$settingsCode = Get-Content -Encoding UTF8 (Join-Path $root 'Views\SettingsWindow.xaml.cs') -Raw
$configCode = Get-Content -Encoding UTF8 (Join-Path $root 'Services\ConfigurationService.cs') -Raw

if ($mainXaml -match 'TopRefraction|LeftRefraction') { throw 'Directional refraction line returned.' }
if ($mainCode -match '유리 배경 끄기|유리 배경 켜기') { throw 'Glass background toggle returned.' }
if ($settingsXaml -match 'GlassBackgroundCheck') { throw 'Glass background checkbox returned.' }
if ($settingsXaml -notmatch '기본 설정 초기화') { throw 'Default reset action is missing.' }
if ($settingsXaml -match 'ThemeStateText|AutoThemeDescription|CustomThemeButton') { throw 'Obsolete color description returned.' }
if ($settingsCode -match 'selectedGlassColor = palette.Glass') { throw 'Color theme overwrites glass tint.' }
if ($mainXaml -notmatch 'controls:GlassCalendar') { throw 'Dedicated calendar is missing.' }
if ($mainXaml -match '<Calendar\s') { throw 'OS calendar template returned to the widget.' }
if ($mainXaml -notmatch 'controls:ClockText' -or $mainCode -notmatch 'PeriodFontSize') { throw 'Measured clock with small AM/PM is missing.' }
if ($settingsCode -notmatch 'WeightCalendar') { throw 'Typography weight editor is missing.' }
if ($configCode -notmatch 'schema_version = 14') { throw 'Configuration schema is not version 14.' }

if ($mainXaml -match '<Viewbox') { throw 'Transform-based widget text scaling returned.' }
if ($mainCode -notmatch 'ApplyLayoutScale') { throw 'Final-size text layout is missing.' }
if ($settingsXaml -notmatch 'VisibleDDayCountInput') { throw 'Visible schedule count is missing.' }
if ($mainXaml -match '<ScrollViewer') { throw 'Widget content scrolling returned.' }
if ($settingsCode -notmatch 'ResetPresentation') { throw 'Schedule-preserving reset is missing.' }

if ($settingsXaml -notmatch 'TextColorModeCombo') { throw 'Text color mode is missing.' }
if ($settingsXaml -notmatch 'WeekStartCombo') { throw 'First weekday choice is missing.' }
if (-not (Test-Path (Join-Path $root 'Services\CalendarAnnotations.cs'))) { throw 'Calendar annotation service is missing.' }

if ($mainXaml -match 'DeveloperBadge|PreviousDDay_OnClick|NextDDay_OnClick|CarouselPosition|CarouselNavigation') { throw 'Obsolete widget chrome returned.' }
if ($mainCode -notmatch 'CreateScheduleCapsuleBrush') { throw 'Schedule capsule background is missing.' }
if ($mainCode -match 'CalendarHost.Background = LiquidGlassTheme.CreateClockCardBrush') { throw 'Calendar background panel returned.' }
if ($settingsXaml -notmatch 'Tag="#DCDCDC"') { throw 'Neutral glass tint is missing.' }
if ($settingsXaml -match '이 영역을 잡고 끌어서 창을 이동하세요') { throw 'Obsolete settings move hint returned.' }
if ($settingsXaml -notmatch 'Width="600"[^\r\n]*MinWidth="560"') { throw 'Compact settings width is missing.' }
if ($settingsXaml -notmatch 'DDaySortCombo' -or $settingsXaml -notmatch 'FontPresetCombo' -or $settingsXaml -match 'TypographyRoleCombo') { throw 'Compact typography table is missing.' }
if ($settingsXaml -notmatch 'ClockOpacityDial' -or $settingsXaml -notmatch 'DDayOpacityDial') { throw 'Independent layer controls are missing.' }
if ($settingsCode -notmatch 'ScheduleOrdering.Sort') { throw 'Persistent list sorting is missing.' }
if ($settingsCode -notmatch 'CreateThemeButtonContent') { throw 'Theme swatches are missing.' }
if ($configCode -notmatch 'show_korean_holidays') { throw 'Holiday opt-in persistence is missing.' }
if ($settingsXaml -notmatch 'LightDirectionDial' -or $configCode -notmatch 'glass_light_direction') { throw 'Directional glass lighting is missing.' }
if ($settingsXaml -notmatch 'ShowSolarTermsCheck' -or $configCode -notmatch 'show_solar_terms') { throw 'Solar-term option is missing.' }
if ($mainXaml -notmatch 'controls:GlassPanel' -or $mainCode -notmatch 'GlassPanel capsule') { throw 'Layered glass surfaces are missing.' }
if ($settingsXaml -notmatch 'PanelDepthSlider' -or $settingsXaml -notmatch 'ClockDepthSlider' -or $settingsXaml -notmatch 'DDayDepthSlider') { throw 'Per-panel relief controls are missing.' }
if ($configCode -notmatch 'clock_panel_depth' -or $configCode -notmatch 'dday_panel_depth') { throw 'Signed relief persistence is missing.' }
if ($settingsCode -notmatch 'CleanDesign_OnClick' -or $settingsCode -notmatch 'GlassDepthPreset.All') { throw 'Clean design or depth presets are missing.' }
if ($settingsXaml -match 'DeveloperTab|DEVELOPER PREVIEW') { throw 'Developer UI remains.' }
if ($settingsCode -match 'TypographyRatio') { throw 'Integer percentage conversion remains.' }
if ($mainCode -notmatch 'ClockSchedule.NextDelay') { throw 'Clock alignment is missing.' }

if ($Configuration -eq 'Release') {
    $releaseExe = Join-Path $root 'bin\x64\Release\DDay3.exe'
    if (-not (Test-Path $releaseExe)) { throw 'Release executable was not found.' }
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($releaseExe)
    if ($assembly.GetType('DDay3.Diagnostics.DeveloperTraceLog', $false)) { throw 'Log backend is present in Release.' }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($releaseExe).FileVersion -ne '3.1.6.0') { throw 'Unexpected executable version.' }
}

Write-Host "[OK] D-Day 3 source verification passed ($Configuration)."
