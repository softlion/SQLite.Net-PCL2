$nugetServer="https://www.nuget.org"
if ($IsMacOS) {
    $msbuild = "msbuild"
} else {
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    $msbuild = join-path $msbuild 'MSBuild\Current\Bin\MSBuild.exe'
}
$version="2.1.0"
$versionSuffix="-pre6"

#####################
#Build release config
cd $PSScriptRoot
del *.nupkg
& $msbuild "../SQLite.Net.sln" /restore /p:Configuration=Release /p:Platform="Any CPU" /p:Version="$version" /p:VersionSuffix="$versionSuffix" /p:Deterministic=false /p:PackageOutputPath="$PSScriptRoot" --% /t:Clean;Build;Pack
if ($lastexitcode -ne 0) { exit $lastexitcode; }

nuget push "sqlite-net2.$version$versionSuffix.nupkg" -Source $nugetServer
#copy "sqlite-net2.$version$versionSuffix.nupkg" "D:\repos\localnugets"
