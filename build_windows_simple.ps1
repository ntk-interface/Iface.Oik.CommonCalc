Get-ChildItem -Path "dist" | Remove-Item -Force -Recurse -ErrorAction Ignore

dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:CopyOutputSymbolsToPublishDirectory=false -o dist