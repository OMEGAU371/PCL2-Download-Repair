param([string]$OutputRoot=(Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    foreach($project in @('Host','DownloadRestore','Bootstrap','Mini')) {
        dotnet build (Join-Path $PSScriptRoot ('src/'+$project+'/'+$project+'.csproj')) -c Release --nologo
        if($LASTEXITCODE -ne 0){throw ($project+' compilation failed.')}
    }
    $hostExe=Join-Path $PSScriptRoot 'src/Host/bin/Release/PclShell.exe'
    $test=Start-Process -FilePath $hostExe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    if($test.ExitCode -ne 0){throw 'Self-tests failed.'}
    $report=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/Host/bin/Release/self-test.json') -Raw|ConvertFrom-Json
    if(!$report.success){throw 'Self-test report failed.'}
    $OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
    New-Item -ItemType Directory -Path $OutputRoot -Force|Out-Null
    $staging=Join-Path $OutputRoot ('staging-'+[Guid]::NewGuid().ToString('N'))
    $hostDirectory=Join-Path $staging 'PCL-Shell-Auto'
    $pluginDirectory=Join-Path $hostDirectory 'plugins/download-restore'
    New-Item -ItemType Directory -Path $pluginDirectory -Force|Out-Null
    foreach($name in @('PclShell.exe','PclShell.exe.config','PclShell.Api.dll')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('src/Host/bin/Release/'+$name)) -Destination $hostDirectory}
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Bootstrap/bin/Release/PclShell.Bootstrap.dll') -Destination $hostDirectory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/DownloadRestore/bin/Release/PclShell.DownloadRestore.dll') -Destination $pluginDirectory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/DownloadRestore/plugin.json') -Destination $pluginDirectory
    foreach($name in @('PCL下载助手.exe','PCL下载助手.exe.config')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('src/Mini/bin/Release/'+$name)) -Destination $staging}
    foreach($name in @('README.md','SECURITY.md','LICENSE','TESTING.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $staging}
    $hashLines=Get-ChildItem -LiteralPath $staging -File -Recurse|Sort-Object FullName|ForEach-Object{
        $relative=$_.FullName.Substring($staging.Length+1).Replace('\','/')
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$relative
    }
    [IO.File]::WriteAllLines((Join-Path $staging 'SHA256SUMS.txt'),$hashLines,[Text.UTF8Encoding]::new($false))
    $zip=Join-Path $OutputRoot 'PCL2-Download-Repair.zip'
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
    $zipHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()+'  PCL2-Download-Repair.zip'
    [IO.File]::WriteAllText((Join-Path $OutputRoot 'PCL2-Download-Repair.zip.sha256'),$zipHash+"`n",[Text.UTF8Encoding]::new($false))
    $report|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputRoot 'self-test.json') -Encoding UTF8
    Write-Host ('Package: '+$zip)
}finally{Pop-Location}
