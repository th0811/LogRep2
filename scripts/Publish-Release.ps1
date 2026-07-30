[CmdletBinding()]
param(
    [string]$Version,
    [switch]$IncludeSelfContained
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\LogRep2.App\LogRep2.App.csproj'

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$project = Get-Content -LiteralPath $projectPath
    $Version = [string]$project.Project.PropertyGroup.Version
}

if ($Version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') {
    throw "バージョンは 1.2.3 形式で指定してください: $Version"
}

$artifactRoot = Join-Path $repositoryRoot 'artifacts'

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

dotnet test (Join-Path $repositoryRoot 'LogRep2.sln') -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'テストに失敗したため、リリース作成を中止しました。'
}

function New-ReleaseArtifact {
    param(
        [Parameter(Mandatory)]
        [string]$ArtifactName,

        [Parameter(Mandatory)]
        [bool]$SelfContained
    )

    $publishDirectory = Join-Path $artifactRoot $ArtifactName
    $zipPath = Join-Path $artifactRoot "$ArtifactName.zip"
    $hashPath = "$zipPath.sha256"

    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    if (Test-Path -LiteralPath $hashPath) {
        Remove-Item -LiteralPath $hashPath -Force
    }

    dotnet publish $projectPath `
        -c Release `
        -r win-x64 `
        --self-contained $SelfContained.ToString().ToLowerInvariant() `
        -p:PublishProfile=win-x64 `
        -p:Version=$Version `
        -p:ContinuousIntegrationBuild=true `
        -o $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "publishに失敗しました: $ArtifactName"
    }

    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $hashPath -Value "$hash *$([IO.Path]::GetFileName($zipPath))" -Encoding ascii

    Write-Host "リリースを作成しました: $zipPath"
    Write-Host "SHA-256: $hash"
}

New-ReleaseArtifact `
    -ArtifactName "LogRep2-$Version-win-x64" `
    -SelfContained $false

if ($IncludeSelfContained) {
    New-ReleaseArtifact `
        -ArtifactName "LogRep2-$Version-win-x64-self-contained" `
        -SelfContained $true
}
