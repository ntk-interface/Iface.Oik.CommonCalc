Get-ChildItem -Path "dist_linux" | Remove-Item -Force -Recurse -ErrorAction Ignore

dotnet publish -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=true -p:CopyOutputSymbolsToPublishDirectory=false -o dist_linux