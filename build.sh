#!/bin/sh

config=$1
if [ ! -n "$config" ]
then
    config="Release"
fi

outputPath=$2
if [ -z "$outputPath" ]
then
    outputPath=$(realpath "./artifacts/")
fi

echo "Config: $config"
echo "Output Path: $outputPath"

if [ ! -d "$outputPath" ]
then
  mkdir -p "$outputPath"
fi

solution=$(realpath "./openmedstack-neventstore.sln")

dotnet clean $solution
dotnet build $solution -c $config
dotnet pack $solution -c $config -o "$outputPath" --include-symbols
