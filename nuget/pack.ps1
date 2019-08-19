######NuGet.CommandLine is too old and does not support Xamarin.iOS libs
#####Please install the package "NuGe t.CommandLine" from https://chocolatey.org/ before running this script
#####After chocolatey is installed, type: choco install NuGet.CommandLine
#####Before running this script, download nuget.exe from @echo https://nuget.codeplex.com/releases/view/133091
#####and put nuget.exe in the path.

#####set /p nugetServer=Enter base nuget server url (with /): 
$nugetServer="https://www.nuget.org"

#####################
#Build release config
cd ..
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
nuget restore
$msbuildparams = '/t:Clean;Build', '/p:Configuration=Release', '/p:Platform=Any CPU', 'SQLite.Net.sln'
& $msbuild $msbuildparams
cd nuget

del *.nupkg

$version="2.0.0"
nuget pack "sqlite-net2.nuspec" -Version $version
nuget push "sqlite-net2.$version.nupkg" -Source $nugetServer

#####set assembly info to version
#####https://gist.github.com/derekgates/4678882
