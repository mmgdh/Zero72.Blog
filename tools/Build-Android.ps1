param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$AndroidSdkDirectory = "$env:LOCALAPPDATA\Android\sdk",
    [string]$JavaSdkDirectory = "$env:LOCALAPPDATA\Android\jdk-17",
    [switch]$IncrementVersion,
    [string]$ReleaseNotes = "Improvements and stability fixes."
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot "src\Zero72.Blog.Mobile\Zero72.Blog.Mobile.csproj"
$androidJar = Join-Path $AndroidSdkDirectory "platforms\android-36\android.jar"
$assetsFile = Join-Path $projectRoot "src\Zero72.Blog.Mobile\obj\project.assets.json"
$projectContent = [System.IO.File]::ReadAllText($project)
$displayVersionPattern = '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>'
$versionCodePattern = '<ApplicationVersion>(\d+)</ApplicationVersion>'
$displayVersionMatch = [regex]::Match($projectContent, $displayVersionPattern)
$versionCodeMatch = [regex]::Match($projectContent, $versionCodePattern)

if (-not $displayVersionMatch.Success -or -not $versionCodeMatch.Success) {
    throw "Android version fields were not found in the project file."
}

$versionName = $displayVersionMatch.Groups[1].Value
$versionCode = [int]$versionCodeMatch.Groups[1].Value
if ($IncrementVersion) {
    $parsedVersion = [version]::Parse($versionName)
    $major = [Math]::Max(0, $parsedVersion.Major)
    $minor = [Math]::Max(0, $parsedVersion.Minor)
    $patch = if ($parsedVersion.Build -ge 0) { $parsedVersion.Build + 1 } else { 1 }
    $versionName = "$major.$minor.$patch"
    $versionCode++
}

# Install Android SDK and JDK into the user profile on the first build.
if (-not (Test-Path -LiteralPath $androidJar) -or -not (Test-Path -LiteralPath $JavaSdkDirectory)) {
    dotnet build $project `
        -t:InstallAndroidDependencies `
        -f net10.0-android `
        -p:AndroidSdkDirectory=$AndroidSdkDirectory `
        -p:JavaSdkDirectory=$JavaSdkDirectory `
        -p:AcceptAndroidSDKLicenses=True `
        -nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Android build dependency installation failed."
    }
}

# Restore once for a fresh checkout when the Android dependencies already exist.
if (-not (Test-Path -LiteralPath $assetsFile)) {
    dotnet restore $project `
        -m:1 `
        -nr:false `
        -p:AndroidSdkDirectory=$AndroidSdkDirectory `
        -p:JavaSdkDirectory=$JavaSdkDirectory `
        -p:UseSharedCompilation=false `
        -nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Android project restore failed."
    }
}

# Build Android with explicit version overrides so incrementing is atomic.
$buildArguments = @(
    "build", $project,
    "-c", $Configuration,
    "-m:1",
    "-nr:false",
    "-p:AndroidSdkDirectory=$AndroidSdkDirectory",
    "-p:JavaSdkDirectory=$JavaSdkDirectory",
    "-p:UseSharedCompilation=false",
    # API 36 currently fails in the out-of-process ILLink task host on this build machine.
    # Disabling trimming and AOT keeps the APK functional and makes repeated deployer builds reliable.
    "-p:PublishTrimmed=false",
    "-p:RunAOTCompilation=false",
    "-p:ApplicationDisplayVersion=$versionName",
    "-p:ApplicationVersion=$versionCode",
    "--no-restore",
    "-nologo"
)
& dotnet @buildArguments

if ($LASTEXITCODE -ne 0) {
    throw "Android $Configuration build failed."
}

$sourceApk = Join-Path $projectRoot "src\Zero72.Blog.Mobile\bin\$Configuration\net10.0-android\com.zero72.blog.mobile-Signed.apk"
$artifactDirectory = Join-Path $projectRoot "artifacts\mobile"
$distributionDirectory = Join-Path $projectRoot "src\Zero72.Blog.ClientHost\wwwroot\mobile"
$safeVersionName = $versionName -replace '[^0-9A-Za-z._-]', '-'
$apkFileName = "Zero72.Blog.Mobile-$safeVersionName.apk"
$targetApk = Join-Path $artifactDirectory $apkFileName
$latestApk = Join-Path $artifactDirectory "Zero72.Blog.Mobile-latest.apk"
$distributionApk = Join-Path $distributionDirectory $apkFileName
$manifestPath = Join-Path $distributionDirectory "latest.json"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $distributionDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceApk -Destination $targetApk -Force
Copy-Item -LiteralPath $sourceApk -Destination $latestApk -Force

# Keep only the latest versioned APK in the server distribution directory.
Get-ChildItem -LiteralPath $distributionDirectory -Filter "Zero72.Blog.Mobile-*.apk" -File |
    Where-Object { $_.Name -ne $apkFileName } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
Copy-Item -LiteralPath $sourceApk -Destination $distributionApk -Force

$manifest = [ordered]@{
    versionCode = $versionCode
    versionName = $versionName
    downloadUrl = "/mobile/$apkFileName"
    releaseNotes = $ReleaseNotes
    publishedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    required = $false
}
$manifestJson = $manifest | ConvertTo-Json
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))

# Persist the increment only after a successful APK build.
if ($IncrementVersion) {
    $displayVersionRegex = [regex]::new($displayVersionPattern)
    $versionCodeRegex = [regex]::new($versionCodePattern)
    $updatedProjectContent = $displayVersionRegex.Replace(
        $projectContent,
        "<ApplicationDisplayVersion>$versionName</ApplicationDisplayVersion>",
        1)
    $updatedProjectContent = $versionCodeRegex.Replace(
        $updatedProjectContent,
        "<ApplicationVersion>$versionCode</ApplicationVersion>",
        1)
    [System.IO.File]::WriteAllText($project, $updatedProjectContent, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Android APK created: $targetApk"
Write-Host "Update manifest created: $manifestPath"
