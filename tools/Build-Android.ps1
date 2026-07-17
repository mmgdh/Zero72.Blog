param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$AndroidSdkDirectory = "$env:LOCALAPPDATA\Android\sdk",
    [string]$JavaSdkDirectory = "$env:LOCALAPPDATA\Android\jdk-17"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot "src\Zero72.Blog.Mobile\Zero72.Blog.Mobile.csproj"
$androidJar = Join-Path $AndroidSdkDirectory "platforms\android-36\android.jar"
$assetsFile = Join-Path $projectRoot "src\Zero72.Blog.Mobile\obj\project.assets.json"

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

# Build the Android project. Release mode produces a signed sideload APK.
dotnet build $project `
    -c $Configuration `
    -m:1 `
    -nr:false `
    -p:AndroidSdkDirectory=$AndroidSdkDirectory `
    -p:JavaSdkDirectory=$JavaSdkDirectory `
    -p:UseSharedCompilation=false `
    --no-restore `
    -nologo

if ($LASTEXITCODE -ne 0) {
    throw "Android $Configuration build failed."
}

$sourceApk = Join-Path $projectRoot "src\Zero72.Blog.Mobile\bin\$Configuration\net10.0-android\com.zero72.blog.mobile-Signed.apk"
$artifactDirectory = Join-Path $projectRoot "artifacts\mobile"
$targetApk = Join-Path $artifactDirectory "Zero72.Blog.Mobile-1.0.0.apk"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceApk -Destination $targetApk -Force
Write-Host "Android APK created: $targetApk"
