param
(
    $config = 'Release',
	$outputPath = ".\artifacts\"
)

If(!(test-path $outputPath))
{
	New-Item -ItemType Directory -Force -Path $outputPath
}

$outputPath = Resolve-Path $outputPath

$solution = Resolve-Path .\openmedstack-neventstore.sln
dotnet clean $solution
dotnet build $solution -c $config
dotnet pack $solution -c $config -o $outputPath --include-symbols
